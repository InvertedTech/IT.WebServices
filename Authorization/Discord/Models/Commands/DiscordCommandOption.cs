using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Commands
{
    public class DiscordCommandOption
    {
        [JsonPropertyName("type")]
        public int Type { get; set; }             // 3=string, 6=user

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("required")]
        public bool Required { get; set; }
    }
}
