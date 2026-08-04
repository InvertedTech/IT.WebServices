using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Events.Generic;
using IT.WebServices.Authorization.Events.Generic.Data;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Fragments.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Combined.Services
{
    [Authorize]
    public class EventService : EventInterface.EventInterfaceBase
    {
        public readonly ILogger Log;
        private readonly IGenericEventRecordProvider eventProvider;
        private readonly IGenericEventTicketRecordProvider ticketProvider;
        private readonly GenericEventProviderProvider genericEventProviderProvider;

        public EventService(ILogger<EventService> log, IGenericEventRecordProvider eventProvider, IGenericEventTicketRecordProvider ticketProvider, GenericEventProviderProvider genericEventProviderProvider)
        {
            Log = log;
            this.eventProvider = eventProvider;
            this.ticketProvider = ticketProvider;
            this.genericEventProviderProvider = genericEventProviderProvider;
        }

        public override async Task<GetEventsResponse> GetEvents(GetEventsRequest request, ServerCallContext context)
        {
            var res = new GetEventsResponse();

            res.Records.AddRange(await eventProvider.GetAll().ToListAsync());
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res.Records.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        public override async Task<GetEventResponse> GetEvent(GetEventRequest request, ServerCallContext context)
        {
            var eventId = request.EventID.ToGuid();
            if (eventId == Guid.Empty)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Invalid EventID") };

            var record = await eventProvider.GetById(eventId);
            if (record == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Event Not Found") };

            return new() { Record = record };
        }

        public override async Task<GetOwnTicketsResponse> GetOwnTickets(GetOwnTicketsRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not Logged In") };

            var res = new GetOwnTicketsResponse();

            res.Records.AddRange(await ticketProvider.GetAllByUserId(user.Id).ToListAsync());
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res.Records.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        public override async Task<ReserveTicketResponse> ReserveTicket(ReserveTicketRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not Logged In") };

            var eventId = request.EventID.ToGuid();
            if (eventId == Guid.Empty)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Invalid EventID") };

            if (request.Quantity == 0)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Quantity Must Be Greater Than Zero") };

            var evt = await eventProvider.GetById(eventId);
            if (evt == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Event Not Found") };

            var ticketClass = evt.TicketClasses.FirstOrDefault(tc => tc.TicketClassID == request.TicketClassID);
            if (ticketClass == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Ticket Class Not Found") };

            var provider = genericEventProviderProvider.GetProcessor(evt);
            var result = await provider.ReserveTicket(evt, ticketClass, user, request.Quantity, context.CancellationToken);

            var res = new ReserveTicketResponse { CheckoutUrl = result.CheckoutUrl };

            if (!result.Success)
            {
                res.Error = result.Error ?? GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Reservation Failed");
                return res;
            }

            res.Tickets.AddRange(result.Tickets);

            return res;
        }
    }
}
