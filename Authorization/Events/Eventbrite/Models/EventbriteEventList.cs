using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Events.Eventbrite.Models
{
    public sealed class EventbriteEventList
    {
        [JsonPropertyName('pagination')]
        public EventbritePagination Pagination { get; set; }
        [JsonPropertyName("events")]
        public List<EventbriteEvent> Events { get; set; }
    }
}
