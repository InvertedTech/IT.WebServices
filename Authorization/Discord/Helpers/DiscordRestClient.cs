using IT.WebServices.Authorization.Discord.Models.Commands;
using IT.WebServices.Authorization.Discord.Models.Interactions;
using IT.WebServices.Authorization.Discord.Models.LinkedRoles;
using IT.WebServices.Authorization.Discord.Models.OAuth;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IT.WebServices.Authorization.Discord.Helpers
{
    // <summary>
    // HttpClient Wrapper For The Discord Rest API
    // </summary>
    public class DiscordRestClient
    {
        private const string V10 = "v10/";

        private readonly HttpClient _http;
        private readonly DiscordSettings _settings;

        public DiscordRestClient(HttpClient http, DiscordSettings settings)
        {
            _http = http;
            _settings = settings;
        }

        // <summary>
        //  Registers slash commands to the Guilds endpoint
        // </summary>
        // <param name="guildId">The Target Guild's Id</param>
        // <param name="commands"> The Slash Commands To Register
        public async ValueTask RegisterCommandsAsync(string guildId, IEnumerable<DiscordApplicationCommand> commands)
        {
            var path = $"{V10}applications/{_settings.AppId}/guilds/{guildId}/commands";
            using StringContent jsonContent = new StringContent(
                    JsonSerializer.Serialize(commands),
                    Encoding.UTF8,
                    "application/json"
                );
            var response = await _http.PutAsync(path, jsonContent);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Discord {response.StatusCode}: {body}");
            }
        }

        public async ValueTask RespondToInteractionAsync(string interactionId, string interactionToken,  InteractionResponse response)
        {
            throw new NotImplementedException();
        }

        public async ValueTask DeferInteractionAsync(string interactionId, string interactionToken, bool ephemeral = false)
        {
            throw new NotImplementedException();
        }

        public async ValueTask EditInteractionResponseAsync(string interactionToken, InteractionCallbackData data)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<DiscordInteractionMember> GetGuildMemberAsync(string guildId, string userId)
        {
            throw new NotImplementedException();
        }

        public async ValueTask AddRoleAsync(string guildId, string userId, string roleId)
        {
            throw new NotImplementedException();
        }

        public async ValueTask RemoveRoleAsync(string guildId, string userId, string roleId)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<string> CreateThreadAsync(string channelId, string name)
        {
            throw new NotImplementedException();
        }

        public async ValueTask LockThreadAsync(string threadId)
        {
            throw new NotImplementedException();
        }

        public async ValueTask ArchiveThreadAsync(string threadId)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<DiscordCurrentUser> GetCurrentUserAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{V10}users/@me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<DiscordCurrentUser>(jsonString);
        }

        public async ValueTask<OAuthTokenResponse> ExchangeCodeAsync(string code)
        {
            var path = "oauth2/token";
            var requestDictionary = new Dictionary<string, string>
            {
                { "client_id", _settings.AppId },
                { "client_secret", _settings.ClientSecret },
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", _settings.OAuthRedirect }
            };

            var content = new FormUrlEncodedContent(requestDictionary);

            var response = await _http.PostAsync(path, content);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OAuthTokenResponse>(jsonString);
        }

        public async ValueTask<OAuthTokenResponse> RefreshTokenAsync(string refreshToken)
        {
            throw new NotImplementedException();
        }


        public async ValueTask RevokeTokenAsync(string token)
        {
            throw new NotImplementedException();
        }

        public async ValueTask PushLinkedRoleMetadataAsync(string userAccessToken, LinkedRoleMetadata metadata)
        {
            throw new NotImplementedException();
        }

        public async ValueTask RegisterRoleMetadataAsync(IEnumerable<RoleMetadataSchema> schema)
        {
            throw new NotImplementedException();
        }
    }
}
