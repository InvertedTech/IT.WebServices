using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Models.Interactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSec.Cryptography;
using System.Security.Cryptography;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        public DiscordController(DiscordCommandRouter commandRouter, DiscordSettings settings, DiscordRestClient client)
        {
            _interactionValidator = new InteractionValidator();
            _commandRouter = commandRouter;
            _settings = settings;
            _client = client;
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
        [HttpGet("oauth/callback")]
        public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string? state)
        {
            //state = CryptoHelper.GenerateHmacSha256State(platformUserId, _settings.DiscordStateSecret);
            //if (!ValidateState(state, out var platformUserId))
            //    return BadRequest("Invalid state");
            var tokenRes = await _client.ExchangeCodeAsync(code);

            var currentUser = await _client.GetCurrentUserAsync(tokenRes.AccessToken);

            return Redirect("/linked-role-success");
        }
    }
}
