using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.OAuth
{
    public class DiscordCurrentUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("global_name")]
        public string GlobalName { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}
