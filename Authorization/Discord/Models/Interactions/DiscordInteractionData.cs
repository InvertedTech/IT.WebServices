using IT.WebServices.Authorization.Discord.Models.Modal;
using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Interactions
{
    public class DiscordInteractionData {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("custom_id")]
        public string? CustomId { get; set; }

        [JsonPropertyName("component_type")]
        public int? ComponentType { get; set; }

        [JsonPropertyName("options")]
        public List<DiscordInteractionOption>? Options { get; set; }

        [JsonPropertyName("components")]
        public List<DiscordModalActionRow>? Components { get; set; }
    }
}