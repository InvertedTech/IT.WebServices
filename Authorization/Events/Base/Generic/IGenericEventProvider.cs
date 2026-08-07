using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authorization.Events;

namespace IT.WebServices.Authorization.Events.Generic
{
    public interface IGenericEventProvider
    {
        string ProcessorName { get; }
        bool IsEnabled { get; }

        Task<CreateEventResult> CreateEvent(GenericEventRecord evt, CancellationToken ct);
        Task<ReserveTicketResult> ReserveTicket(GenericEventRecord evt, GenericTicketClassRecord ticketClass, ONUser user, uint quantity, CancellationToken ct);
        Task<CancelTicketResult> CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken ct);
        Task<SyncResult> SyncTicket(string processorEventID, string processorTicketID, CancellationToken ct);
        Task<EventSyncResult> SyncEvents(DateTime changedSince, CancellationToken ct);
    }

    public class CreateEventResult
    {
        public string ProcessorEventID { get; set; } = "";
        public string Url { get; set; } = "";
        public Dictionary<string, string> ProcessorTicketClassIDs { get; set; } = new();
        public APIError? Error { get; set; }
    }

    public class CancelTicketResult
    {
        public APIError? Error { get; set; }
    }

    public class EventSyncResult
    {
        public List<GenericEventRecord> Records { get; set; } = new();
        public bool HasMore { get; set; }
        public APIError? Error { get; set; }
    }
}