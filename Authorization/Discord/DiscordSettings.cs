namespace IT.WebServices.Authorization.Discord
{
    public class DiscordSettings
    {
        public const string SectionName = "DiscordSettings";
        public readonly string DiscordUri  = "https://discord.com/api/";
        public string AppId { get; init; }
        public string BotToken { get; init; }
        public string PublicKey { get; init; }
        public string ClientSecret { get; init; }
        public string OAuthRedirect { get; init; }
        public string DiscordStateSecret { get; init; }
    }
}
