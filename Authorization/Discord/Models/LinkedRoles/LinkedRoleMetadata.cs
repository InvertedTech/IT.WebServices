namespace IT.WebServices.Authorization.Discord.Models.LinkedRoles
{
    public class LinkedRoleMetadata
    {
        public string PlatformName;             // e.g. "Inverted"
        public string PlatformUsername;         // display name on the platform
        public Dictionary<string, string> Metadata;  // keys match RegisterRoleMetadataAsync schema
    }
}
