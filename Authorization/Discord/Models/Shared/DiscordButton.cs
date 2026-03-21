namespace IT.WebServices.Authorization.Discord.Models.Shared
{
    public class DiscordButton
    {
        public int Type = 2;
        public int Style;                         // 1=Primary, 2=Secondary, 4=Danger
        public string Label;
        public string CustomId;
    }
}
