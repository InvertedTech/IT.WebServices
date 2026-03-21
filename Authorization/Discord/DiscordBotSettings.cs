namespace IT.WebServices.Authorization.Discord
{
    public class DiscordBotSettings
    {
        public const string SectionName = "BotSettings";
        public string GuildId { get; init; } = string.Empty;
    }
}
