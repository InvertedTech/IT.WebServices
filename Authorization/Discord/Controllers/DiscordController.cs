using IT.WebServices.Authentication;
using IT.WebServices.Authentication.Services.Data;
using IT.WebServices.Authentication.Services.Helpers;
using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Models.Interactions;
using IT.WebServices.Clients.Authentication;
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
        private readonly IUserDataProvider _userDataProvider;
        private readonly TokenHelper _tokenHelper;

        public DiscordController(DiscordCommandRouter commandRouter, DiscordSettings settings, DiscordRestClient client, ILogger<DiscordController> logger, UserClient users, IUserDataProvider userDataProvider, TokenHelper tokenHelper)
        {
            _interactionValidator = new InteractionValidator();
            _commandRouter = commandRouter;
            _settings = settings;
            _client = client;
            _logger = logger;
            _users = users;
            _userDataProvider = userDataProvider;
            _tokenHelper = tokenHelper;
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
        public IActionResult SignInWithDiscord()
        {
            throw new NotImplementedException();
        }

        [AllowAnonymous]
        [HttpGet("oauth/callback")]
        public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string? state)
        {
            throw new NotImplementedException();
        }
    }
}
