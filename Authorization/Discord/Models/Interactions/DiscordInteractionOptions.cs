using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Interactions
{
    public class DiscordInteractionOption {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }             // 3=string, 4=int, 5=bool, 6=user

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }
}