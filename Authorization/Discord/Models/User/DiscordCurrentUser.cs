using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.User
{
    class DiscordCurrentUser
    {
        [JsonPropertyName("id")] string Id { get; set; }
        [JsonPropertyName("username")] string Username { get; set; }
        [JsonPropertyName("global_name")] string GlobalName { get; set; }
        [JsonPropertyName("email")] string? Email { get; set; }
    }
}
