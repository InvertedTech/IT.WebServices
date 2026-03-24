using IT.WebServices.Fragments.Authorization.Discord;
using IT.WebServices.Settings;

namespace IT.WebServices.Authorization.Discord.Helpers
{
    public class DiscordSettingsBuilder
    {
        private readonly SettingsClient _client;

        public DiscordSettingsBuilder(SettingsClient client)
        {
            _client = client;
        }

        public DiscordSettings Build()
        {
            var pub = _client.PublicData;
            if (!pub.Discord.Enabled)
            {
                return new DiscordSettings {
                    AppId = string.Empty,
                    PublicKey = string.Empty,
                    BotToken = string.Empty,
                    OAuthRedirect = string.Empty,
                    ClientSecret =string.Empty,
                    DiscordStateSecret = string.Empty,
                };
            }

            var priv = _client.PrivateData;
            var owner = _client.OwnerData;

            if (priv.Discord == null || owner.Discord == null)
            {
                throw new InvalidOperationException(
                    "Cannot build DiscordSettings: Remote configuration data is missing.");
            }

            return new DiscordSettings
            {
                AppId = priv?.Discord.AppId,
                PublicKey = priv?.Discord.PublicKey,
                OAuthRedirect = priv?.Discord.OAuthRedirectUri,
                SignInSuccessRedirect = priv?.Discord.SignInSuccessRedirect,
                LinkedRoleSuccessRedirect = priv?.Discord.LinkedRoleSuccessRedirect,
                GuildId = priv?.Discord.GuildId,
                SupportChannelId = priv?.Discord.SupportChannelId,
                ShunChannelId = priv?.Discord.ShunChannelId,
                Roles = priv?.Discord.Roles ?? new(),
                CommandNameOverrides = priv?.Discord.CommandNameOverrides ?? new(),
                ClientSecret = owner?.Discord.ClientSecret,
                DiscordStateSecret = owner?.Discord.DiscordStateSecret,
                BotToken = owner?.Discord.BotToken,
            };
        }
    }
}
