using Grpc.Core;
using IT.WebServices.Fragments.Authorization.Events.Eventbrite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Events.Eventbrite.Services
{
    public class EventbriteService : EventbriteEventInterface.EventbriteEventInterfaceBase
    {
        private readonly ILogger log;

        public EventbriteService(ILogger<EventbriteService> log)
        {
            this.log = log;
        }

        public override async Task<EventbriteGetEventResponse> EventbriteGetEvent(EventbriteGetEventRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override async Task<EventbriteGetEventsResponse> EventbriteGetEvents(EventbriteGetEventsRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override async Task<EventbriteGetOwnTicketsResponse> EventbriteGetOwnTickets(EventbriteGetOwnTicketsRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override async Task<EventbriteReserveTicketResponse> EventbriteReserveTicket(EventbriteReserveTicketRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }
    }
}
