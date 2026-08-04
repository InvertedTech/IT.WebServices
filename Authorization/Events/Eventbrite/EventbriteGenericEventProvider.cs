using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Events.Generic;
using IT.WebServices.Fragments.Authorization.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Eventbrite
{
    // No Eventbrite HTTP client/auth exists yet (see EVENTS_EVENTBRITE_PLAN.md Phase 3).
    // IsEnabled is hardcoded false so nothing can silently depend on this working;
    // every method throws until the real client is built.
    public class EventbriteGenericEventProvider : IGenericEventProvider
    {
        public string ProcessorName => "eventbrite";
        public bool IsEnabled => false;

        public Task<string> CreateEvent(GenericEventRecord evt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Eventbrite integration is not implemented yet.");
        }

        public Task<ReserveTicketResult> ReserveTicket(GenericEventRecord evt, GenericTicketClassRecord ticketClass, ONUser user, uint quantity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Eventbrite integration is not implemented yet.");
        }

        public Task<bool> CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Eventbrite integration is not implemented yet.");
        }

        public Task<SyncResult> SyncTicket(string processorTicketId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Eventbrite integration is not implemented yet.");
        }
    }
}
