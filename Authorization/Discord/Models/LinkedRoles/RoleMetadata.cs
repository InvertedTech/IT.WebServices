namespace IT.WebServices.Authorization.Discord.Models.LinkedRoles
{
    public class RoleMetadataSchema
    {
        public int Type;               // 2=number_gt, 7=boolean_equal
        public string Key;             // e.g. "subscription_level"
        public string Name;            // display name shown in Discord
        public string Description;
    }
}
