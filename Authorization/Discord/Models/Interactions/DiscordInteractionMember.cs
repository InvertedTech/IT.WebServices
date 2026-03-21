using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Interactions
{
    public class DiscordInteractionMember
    {
        [JsonPropertyName("user")]
        public DiscordInteractionUser User { get; set; }

        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; }   // Snowflake role IDs — used for permission checks
    }
}
