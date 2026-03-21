using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.Commands
{
    public class DiscordApplicationCommand
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; } = 1;        // 1 = CHAT_INPUT (slash command)

        [JsonPropertyName("options")]
        public List<DiscordCommandOption>? Options { get; set; }

        // null = everyone, "0" = no one, "8" = admin only
        [JsonPropertyName("default_member_permissions")]
        public string? DefaultMemberPermissions { get; set; }
    }
}
