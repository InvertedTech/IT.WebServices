using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Interactions
{
    public class DiscordInteraction
    {
        [JsonPropertyName("type")]
        public int Type { get; set; }             // 1=PING, 2=COMMAND, 3=COMPONENT, 5=MODAL_SUBMIT

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; }

        [JsonPropertyName("data")]
        public DiscordInteractionData? Data { get; set; }

        [JsonPropertyName("member")]
        public DiscordInteractionMember? Member { get; set; }

        [JsonPropertyName("guild_id")]
        public string? GuildId { get; set; }

        [JsonPropertyName("channel_id")]
        public string? ChannelId { get; set; }
    }
}