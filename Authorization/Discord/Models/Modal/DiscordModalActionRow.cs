namespace IT.WebServices.Authorization.Discord.Models.Modal
{
    public class DiscordModalActionRow
    {
        public int Type;                          // Always 1
        public List<DiscordModalTextInput> Components;
    }
}
