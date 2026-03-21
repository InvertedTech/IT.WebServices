namespace IT.WebServices.Authorization.Discord.Models.LinkedRoles
{
    public class RoleMetadataSchema
    {
        public RoleConnectionMetadataType Type;
        public string Key;             // e.g. "subscription_level"
        public string Name;            // display name shown in Discord
        public string Description;
    }
}
