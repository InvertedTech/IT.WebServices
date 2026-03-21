namespace IT.WebServices.Authorization.Discord.Models.Shared
{
    public class DiscordEmbed
    {
        public string? Title;
        public string? Description;
        public int? Color;                        // RGB as int
        public List<DiscordEmbedField>? Fields;
        public string? Timestamp;                 // ISO 8601
    }
}
