using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authorization.Events;
using System.Collections.Generic;

namespace IT.WebServices.Authorization.Events.Generic
{
    public class ReserveTicketResult
    {
        public bool Success { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public List<GenericEventTicketRecord> Tickets { get; set; } = new();
        public APIError? Error { get; set; }
    }
}
