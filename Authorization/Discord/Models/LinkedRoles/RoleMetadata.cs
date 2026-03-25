using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Discord.Models.LinkedRoles
{
    public class RoleMetadataSchema
    {
        [JsonPropertyName("type")]
        public RoleConnectionMetadataType Type { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
}
