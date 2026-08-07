using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Events.Eventbrite.Models
{
    public sealed class EventbriteDateTimeTz
    {
        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }
        [JsonPropertyName("utc")]
        public string UTC { get; set; }
        [JsonPropertyName("local")]
        public string Local { get; set; }
    }
}
