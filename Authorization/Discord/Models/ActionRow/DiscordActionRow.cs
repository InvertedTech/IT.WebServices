using IT.WebServices.Authorization.Discord.Models.Shared;

namespace IT.WebServices.Authorization.Discord.Models.ActionRow
{
    public class DiscordActionRow
    {
        int Type = 1;
        List<DiscordButton> Components;
    }
}
