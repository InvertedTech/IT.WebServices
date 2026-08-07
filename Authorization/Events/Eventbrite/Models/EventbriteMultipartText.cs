using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Events.Eventbrite.Models
{
    public sealed class EventbriteMultipartText
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
        [JsonPropertyName("html")]
        public string Html { get; set; }
    }
}
