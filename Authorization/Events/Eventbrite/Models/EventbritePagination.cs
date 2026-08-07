using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Events.Eventbrite.Models
{
    public sealed class EventbritePagination
    {
        [JsonPropertyName("object_count")]
        public int? ObjectCount { get; set; }
        [JsonPropertyName("page_number")]
        public int PageNumber { get; set; }
        [JsonPropertyName("page_size")]
        public int PageSize { get; set; }
        [JsonPropertyName("page_count")]
        public int? PageCount { get; set; }
        [JsonPropertyName("continuation")]
        public string Continuation { get; set; }
        [JsonPropertyName("has_more_items")]
        public bool HasMoreItems { get; set; }
    }
}
