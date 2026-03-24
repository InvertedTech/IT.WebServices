using IT.WebServices.Fragments.Authorization.Discord;

namespace IT.WebServices.Authorization.Discord
{
    public class DiscordSettings
    {
        public const string SectionName = "DiscordSettings";
        public readonly string DiscordUri = "https://discord.com/api/";
        public string AppId { get; init; }
        public string BotToken { get; init; }
        public string PublicKey { get; init; }
        public string ClientSecret { get; init; }
        public string OAuthRedirect { get; init; }
        public string DiscordStateSecret { get; init; }
        public string SignInSuccessRedirect { get; init; }
        public string LinkedRoleSuccessRedirect { get; init; }
        public string GuildId { get; init; }
        public string SupportChannelId { get; init; }
        public string ShunChannelId { get; init; }
        public IReadOnlyDictionary<string, DiscordRoleRecord> Roles { get; init; }
            = new Dictionary<string, DiscordRoleRecord>();
        public IReadOnlyDictionary<string, string> CommandNameOverrides { get; init; }
            = new Dictionary<string, string>();

        public string GetRoleId(string key)
            => Roles.TryGetValue(key, out var r) ? r.RoleId : string.Empty;

        public string GetCommandName(string defaultName)
            => CommandNameOverrides.TryGetValue(defaultName, out var name) ? name : defaultName;
    }
}
