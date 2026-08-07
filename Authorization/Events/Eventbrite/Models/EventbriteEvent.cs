using IT.WebServices.Authorization.Events.Eventbrite.Models;
using System.Text.Json.Serialization;

namespace IT.WebServices.Authorization.Events.Eventbrite.Models
{
    public sealed class EventbriteEvent
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";

        // Public fields — visible to all users
        [JsonPropertyName("name")] public EventbriteMultipartText? Name { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("description")] public EventbriteMultipartText? Description { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("start")] public EventbriteDateTimeTz? Start { get; set; }
        [JsonPropertyName("end")] public EventbriteDateTimeTz? End { get; set; }
        [JsonPropertyName("created")] public DateTimeOffset Created { get; set; }
        [JsonPropertyName("changed")] public DateTimeOffset Changed { get; set; }
        [JsonPropertyName("published")] public DateTimeOffset? Published { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("online_event")] public bool OnlineEvent { get; set; }
        [JsonPropertyName("hide_start_date")] public bool HideStartDate { get; set; }
        [JsonPropertyName("hide_end_date")] public bool HideEndDate { get; set; }

        // Reference IDs — the expanded objects are omitted unless requested
        [JsonPropertyName("organization_id")] public string? OrganizationId { get; set; }
        [JsonPropertyName("organizer_id")] public string? OrganizerId { get; set; }
        [JsonPropertyName("venue_id")] public string? VenueId { get; set; }

        // Series
        [JsonPropertyName("is_series")] public bool? IsSeries { get; set; }
        [JsonPropertyName("is_series_parent")] public bool? IsSeriesParent { get; set; }
        [JsonPropertyName("series_id")] public string? SeriesId { get; set; }

        // Private fields — present when the token owns the event
        [JsonPropertyName("listed")] public bool? Listed { get; set; }
        [JsonPropertyName("shareable")] public bool? Shareable { get; set; }
        [JsonPropertyName("invite_only")] public bool? InviteOnly { get; set; }
        [JsonPropertyName("show_remaining")] public bool? ShowRemaining { get; set; }
        [JsonPropertyName("password")] public string? Password { get; set; }
        [JsonPropertyName("capacity")] public int? Capacity { get; set; }
        [JsonPropertyName("capacity_is_custom")] public bool? CapacityIsCustom { get; set; }
    }
}