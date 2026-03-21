using IT.WebServices.Authorization.Discord.Models.ActionRow;
using IT.WebServices.Authorization.Discord.Models.Shared;
using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Interactions
{
    public class InteractionCallbackData
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("embeds")]
        public List<DiscordEmbed>? Embeds { get; set; }

        [JsonPropertyName("components")]
        public List<DiscordActionRow>? Components { get; set; }

        [JsonPropertyName("flags")]
        public int? Flags { get; set; }
    }
}
