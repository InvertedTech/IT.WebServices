using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Interactions
{
    public class InteractionResponse
    {
        [JsonPropertyName("type")]
        public int Type { get; set; }             // 1=PONG, 4=CHANNEL_MESSAGE, 5=DEFERRED, 6=DEFERRED_UPDATE

        [JsonPropertyName("data")]
        public InteractionCallbackData? Data { get; set; }
    }
}
