using IT.WebServices.Authentication;
using IT.WebServices.Authentication.Services.Helpers;
using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Models.Interactions;
using IT.WebServices.Authorization.Discord.Models.OAuth;
using IT.WebServices.Authorization.Discord.Data;
using IT.WebServices.Clients.Authentication;
using IT.WebServices.Clients.Payments;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Fragments.Authorization.Discord;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace IT.WebServices.Authorization.Discord.Controllers
{
    [ApiController]
    [Route("/api/discord")]
    public class DiscordController : Controller
    {
        private readonly InteractionValidator _interactionValidator;
        private readonly DiscordCommandRouter _commandRouter;
        private readonly DiscordSettings _settings;
        private readonly DiscordRestClient _client;
        private readonly ILogger _logger;
        private readonly UserClient _users;
        private readonly TokenHelper _tokenHelper;
        private readonly IMemberDataProvider _members;
        private readonly PaymentClient _payments;

        public DiscordController(DiscordCommandRouter commandRouter, DiscordSettings settings, DiscordRestClient client, ILogger<DiscordController> logger, UserClient users, TokenHelper tokenHelper, IMemberDataProvider members, PaymentClient payments)
        {
            _interactionValidator = new InteractionValidator();
            _commandRouter = commandRouter;
            _settings = settings;
            _client = client;
            _logger = logger;
            _users = users;
            _tokenHelper = tokenHelper;
            _members = members;
            _payments = payments;
        }

        [HttpPost("interactions")]
        public async Task<IActionResult> Interactions()
        {
            Request.EnableBuffering();
            var rawBody = await new StreamReader(Request.Body).ReadToEndAsync();
            Request.Body.Position = 0;

            var timestamp = Request.Headers["X-Signature-Timestamp"].ToString();
            var signature = Request.Headers["X-Signature-Ed25519"].ToString();

            if (!_interactionValidator.IsValid(rawBody, timestamp, signature, _settings.PublicKey))
                return Unauthorized();

            var interaction = JsonSerializer.Deserialize<DiscordInteraction>(rawBody);

            return interaction.Type switch
            {
                1 => Ok(new InteractionResponse { Type = 1 }),
                2 => Ok(await _commandRouter.RouteAsync(interaction)),
                _ => BadRequest()
            };
        }

        [AllowAnonymous]
        [HttpGet("oauth/signin")]
        public IActionResult SignInWithDiscord([FromQuery] string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(returnUrl))
                Response.Cookies.Append("discord_return_url", returnUrl, new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(10),
                    IsEssential = true,
                });

            var state = CryptoHelper.GenerateSignInState(_settings.DiscordStateSecret);
            var url = $"https://discord.com/oauth2/authorize"
                    + $"?client_id={_settings.AppId}"
                    + $"&redirect_uri={Uri.EscapeDataString(_settings.OAuthRedirect)}"
                    + $"&response_type=code"
                    + $"&scope=identify%20email"
                    + $"&state={state}";
            return Redirect(url);
        }

        [AllowAnonymous]
        [HttpGet("oauth/callback")]
        public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string? state)
        {
            if (!CryptoHelper.TryValidateState(state, _settings.DiscordStateSecret, out var platformUserId))
                return BadRequest("Invalid state");

            var tokens = await _client.ExchangeCodeAsync(code);
            var discordUser = await _client.GetCurrentUserAsync(tokens.AccessToken);

            if (platformUserId == null)
                return await HandleSignIn(discordUser, tokens);
            else
                return await HandleLink(platformUserId, discordUser, tokens);
        }
        private async Task<IActionResult> HandleSignIn(DiscordCurrentUser discordUser, OAuthTokenResponse tokens)
        {
            var foundUser = await _users.GetUserByEmailAsync(discordUser.Email);
            if (foundUser == null)
                return NotFound(new APIError
                {
                    Reason = APIErrorReason.ErrorReasonNotFound,
                    Message = "No platform account found for this Discord email. Please register first."
                });

            await _users.LinkDiscordAsync(
                    foundUser.UserID,
                    discordUser.Id,
                    tokens.AccessToken,
                    tokens.RefreshToken,
                    DateTime.UtcNow.AddSeconds(tokens.ExpiresIn)
                );

            var jwt = await _tokenHelper.GenerateToken(foundUser.UserID.ToGuid());
            if (string.IsNullOrEmpty(jwt))
                return Unauthorized();

            Response.Cookies.Append(JwtExtensions.JWT_COOKIE_NAME, jwt, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddDays(21),
                IsEssential = true,
            });

            // Redirect to the Linked Roles flow so the member record gets created
            var returnUrl = Request.Cookies["discord_return_url"] ?? _settings.LinkedRoleSuccessRedirect ?? "/";
            Response.Cookies.Delete("discord_return_url");

            var linkUrl = $"/api/discord/oauth/link?returnUrl={Uri.EscapeDataString(returnUrl)}";
            return Redirect(linkUrl);
        }

        [HttpGet("oauth/link")]
        public IActionResult LinkWithDiscord([FromQuery] string? returnUrl = null)
        {
            var userToken = ONUserHelper.ParseUser(HttpContext);
            if (userToken is null) return Unauthorized();

            if (!string.IsNullOrEmpty(returnUrl))
                Response.Cookies.Append("discord_return_url", returnUrl, new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(10),
                    IsEssential = true,
                });

            var state = CryptoHelper.GenerateHmacSha256State(userToken.Id.ToString(), _settings.DiscordStateSecret);
            var url = $"https://discord.com/oauth2/authorize"
                    + $"?client_id={_settings.AppId}"
                    + $"&redirect_uri={Uri.EscapeDataString(_settings.OAuthRedirect)}"
                    + $"&response_type=code"
                    + $"&scope=identify%20role_connections.write"
                    + $"&state={state}";
            return Redirect(url);
        }

        [HttpDelete("link")]
        public async Task<IActionResult> Unlink()
        {
            var userToken = ONUserHelper.ParseUser(HttpContext);
            if (userToken is null) return Unauthorized();

            var member = await _members.GetByUserId(userToken.Id);

            // TODO: revoke the Discord access/refresh token once we have a way to retrieve it
            // (Auth_User.DiscordRefreshToken is in UserServerRecord, not exposed via existing gRPC)

            await _users.UnlinkDiscordAsync(userToken.Id.ToString());

            if (member != null)
                await _members.Delete(userToken.Id);

            return NoContent();
        }

        private async Task<IActionResult> HandleLink(string platformUserId, DiscordCurrentUser discordUser, OAuthTokenResponse tokens)
        {
            var userId = Guid.Parse(platformUserId);
            var now = DateTime.UtcNow;

            var member = await _members.GetByUserId(userId) ?? new DiscordMemberRecord
            {
                UserId = platformUserId,
                Public = new DiscordMemberPublicRecord
                {
                    CreatedOnUTC = Timestamp.FromDateTime(now),
                },
                Private = new DiscordMemberPrivateRecord
                {
                    CreatedById = platformUserId,
                }
            };

            member.Public.DiscordUserId = discordUser.Id;
            member.Public.DiscordUserName = discordUser.GlobalName ?? discordUser.Username;
            member.Public.ModifiedOnUTC = Timestamp.FromDateTime(now);
            member.Private.ModifiedById = platformUserId;
            member.Private.AccessToken = tokens.AccessToken;
            member.Private.RefreshToken = tokens.RefreshToken;
            member.Private.AccessTokenExpiresOnUTC = Timestamp.FromDateTime(now.AddSeconds(tokens.ExpiresIn));

            var subscription = await _payments.GetActiveSubscription(platformUserId);
            if (subscription != null)
                member.Private.InternalSubscriptionId = subscription.SubscriptionRecord.InternalSubscriptionID;

            // TODO: derive tiers from subscription amount/tier config and populate member.Public.Tiers
            // TODO: PushLinkedRoleMetadataAsync
            // TODO: ReconcileRolesAsync

            await _members.Save(member);

            var returnUrl = Request.Cookies["discord_return_url"]
                ?? _settings.LinkedRoleSuccessRedirect
                ?? _settings.SignInSuccessRedirect
                ?? "/";
            Response.Cookies.Delete("discord_return_url");

            return Redirect(returnUrl);
        }
    }
}
