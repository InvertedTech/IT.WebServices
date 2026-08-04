using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authorization.Events;

namespace IT.WebServices.Authorization.Events.Generic
{
    public class SyncResult
    {
        public bool Success { get; set; }
        public GenericEventTicketRecord? Ticket { get; set; }
        public APIError? Error { get; set; }
    }
}
