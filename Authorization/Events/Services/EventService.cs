using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Events.Data;
using IT.WebServices.Authorization.Events.Extensions;
using IT.WebServices.Authorization.Events.Helpers;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Helpers;
using IT.WebServices.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Services
{
    [Authorize]
    public class EventService : EventInterface.EventInterfaceBase
    {
        private readonly ILogger<EventService> _logger;
        private readonly IEventDataProvider _eventProvider;
        private readonly ITicketDataProvider _ticketProvider;
        private readonly ONUserHelper _userHelper;
        private readonly EventTicketClassHelper _ticketClassHelper;

        public EventService(
            ILogger<EventService> logger,
            IEventDataProvider eventProvider,
            ITicketDataProvider ticketProvider,
            ONUserHelper userHelper,
            EventTicketClassHelper ticketClassHelper
        )
        {
            _logger = logger;
            _eventProvider = eventProvider;
            _ticketProvider = ticketProvider;
            _userHelper = userHelper;
            _ticketClassHelper = ticketClassHelper;
        }

        public override async Task<GetEventResponse> GetEvent(GetEventRequest request, ServerCallContext context)
        {

            Guid.TryParse(request.EventId, out var eventId);
            if (eventId == Guid.Empty)
                return new GetEventResponse()
                {
                    Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonInvalidRequest, "Invalid Event ID")
                };

            var rec = await _eventProvider.GetById(eventId);
            if (rec == null)
            {
                return new GetEventResponse()
                {
                    Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonNotFound, $"Event '{eventId}' not found")
                };
            }

            return  new GetEventResponse()
            {
                Event = rec.GetPublicRecord(),
                Error = null // Success case - no error
            };
        }

        public override async Task<GetEventsResponse> GetEvents(GetEventsRequest request, ServerCallContext context)
        {
            var classes = _ticketClassHelper.GetAll();
            var res = new GetEventsResponse();
            var enumerator = _eventProvider.GetEvents();

            // TODO: Make this more efficient
            await foreach (var item in enumerator)
            {
                switch (item.OneOfType)
                {
                     case EventRecordOneOfType.EventOneOfSingle:
                        res.Events.Add(new EventPublicRecord()
                        {
                            EventId = item.EventId,
                            SinglePublic = item.SinglePublic,
                        });
                        break;
                    case EventRecordOneOfType.EventOneOfRecurring:
                        res.Events.Add(new EventPublicRecord()
                        {
                            EventId = item.EventId,
                            RecurringPublic = item.RecurringPublic,
                        });
                        break;
                    default:
                        _logger.LogWarning("Unknown event type encountered: {Type}", item.OneOfType);
                        break;
                }
            }

            res.Error = null; // Success case - no error
            return res;
        }

        public override async Task<GetOwnTicketResponse> GetOwnTicket(GetOwnTicketRequest request, ServerCallContext context)
        {
            var res = new GetOwnTicketResponse();
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var tickets = await _ticketProvider.GetAllByUser(user.Id).ToList();
            var foundTicket = tickets.FirstOrDefault(t => t.TicketId == request.TicketId);

            res.Record = foundTicket.Public;
            return res;
        }

        public override async Task<GetOwnTicketsResponse> GetOwnTickets(GetOwnTicketsRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            var tickets = await _ticketProvider.GetAllByUser(user.Id).ToList();
            var res  = new GetOwnTicketsResponse();
            res.Records.AddRange(tickets.Select(t => t.Public));
            return res;
        }

        public override async Task<CancelOwnTicketResponse> CancelOwnTicket(CancelOwnTicketRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            var res = new CancelOwnTicketResponse();

            var tickets = await _ticketProvider.GetAllByUser(user.Id).ToList();
            var foundTicket = tickets.FirstOrDefault(t => t.TicketId == request.TicketId);
            if (foundTicket == null)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonNotFound, $"Ticket '{request.TicketId}' not found");
                return res;
            }

            foundTicket = foundTicket.Cancel(user.Id.ToString(), request.Reason);

           var success = await _ticketProvider.Update(foundTicket);
            if (!success)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred while canceling ticket");
                return res;
            }
            res.Error = null; // Success case - no error
            return res;
        }

        // TODO: Handle Event Count Update
        public override async Task<ReserveTicketForEventResponse> ReserveTicketForEvent(ReserveTicketForEventRequest request, ServerCallContext context)
        {
            var res = new ReserveTicketForEventResponse();
            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonUnauthorized, "Unauthorized to reserve ticket");
                return res;
            }

            Guid.TryParse(request.EventId, out var eventId);
            if (eventId == Guid.Empty)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonInvalidRequest, "Invalid Event ID");
                return res;
            }

            var eventRecord = await _eventProvider.GetById(eventId);
            if (eventRecord == null)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonNotFound, $"Event '{eventId}' not found");
                return res;
            }

            var ticketClass = _ticketClassHelper.GetById(request.TicketClassId);
            if (ticketClass == null)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonInvalidRequest, "Invalid Ticket Class ID");
                return res;
            }

            // TODO: Rework generation to include the EventTicketClass
            //if (!ticketClass.HasRequestedAmount((int) request.Quantity))
            //{
            //    res.Error = new TicketError()
            //    {
            //        ReserveTicketError = ReserveTicketErrorType.ReserveTicketInvalidRequest,
            //        Message = "No tickets available for this class",
            //    };
            //    return res;
            //}

            //var ticketsReservedByUser = 0;
            //await foreach (var ticket in _ticketProvider.GetAllByUser(user.Id))
            //{
            //    if (ticket.Public.EventId == eventId.ToString())
            //    {
            //        ticketsReservedByUser++;
            //    }
            //}

            //var ticketLimitHit = ticketClass.HitReservationLimit((int)request.Quantity, ticketsReservedByUser);

            //if (ticketLimitHit)
            //{
            //    res.Error = new TicketError()
            //    {
            //        ReserveTicketError = ReserveTicketErrorType.ReserveTicketMaxLimitReached,
            //        Message = $"You can only reserve {ticketClass.MaxTicketsPerUser} tickets per user",
            //    };
            //    return res;
            //}

            //if (!ticketClass.IsOnSale())
            //{
            //    res.Error = new TicketError()
            //    {
            //        ReserveTicketError = ReserveTicketErrorType.ReserveTicketNotOnSale,
            //        Message = "Tickets are not on sale at this time",
            //    };
            //    return res;
            //}

            //var ticketsToReserve = EventTicketRecord.GenerateRecords((int) request.Quantity, eventRecord, user.Id.ToString(), ticketClass);
            //if (ticketsToReserve.Count == 0)
            //{
            //    res.Error = new TicketError()
            //    {
            //        ReserveTicketError = ReserveTicketErrorType.ReserveTicketUnknown,
            //        Message = "Unknown Error Has Occured"
            //    };
            //    return res;
            //}

            //var success = await _ticketProvider.Create(ticketsToReserve);
            //if (!success)
            //{
            //    res.Error = new TicketError()
            //    {
            //        ReserveTicketError = ReserveTicketErrorType.ReserveTicketUnknown,
            //        Message = "Unknown Error Has Occured"
            //    };
            //    return res;
            //}

            //res.Tickets.AddRange(ticketsToReserve);
            
            res.Error = null; // Success case - no error
            return res;
        }

        public override async Task<UseTicketResponse> UseTicket(UseTicketRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            var res = new UseTicketResponse();  
            var tickets = await _ticketProvider.GetAllByUser(user.Id).ToList();
            var foundTicket = tickets.FirstOrDefault(t => t.TicketId == request.TicketId);

            if (foundTicket == null)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonNotFound, $"Ticket '{request.TicketId}' not found");
                return res;
            }

            if (foundTicket.Public.Status == EventTicketStatus.TicketStatusUsed)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonConflict, $"Ticket '{request.TicketId}' is already used");
                return res;
            }

            if (foundTicket.Public.Status == EventTicketStatus.TicketStatusCanceled)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonConflict, $"Ticket '{request.TicketId}' is canceled");
                return res;
            }

            if (foundTicket.Public.Status == EventTicketStatus.TicketStatusExpired)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonInvalidDate, $"Ticket '{request.TicketId}' is expired");
                return res;
            }

            foundTicket = foundTicket.MarkAsUsed(user.Id.ToString());

            var success = await _ticketProvider.Update(foundTicket);
            if (!success)
            {
                res.Error = GenericErrorExtensions.Create(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred while using ticket");
                return res;
            }

            res.Error = null; // Success case - no error
            return res;
        }
    }
}
