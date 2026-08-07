using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Events.Generic;
using IT.WebServices.Authorization.Events.Generic.Data;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Fragments.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Combined
{
    public class BuiltInGenericEventProvider : IGenericEventProvider
    {
        private readonly IGenericEventTicketRecordProvider ticketProvider;

        public BuiltInGenericEventProvider(IGenericEventTicketRecordProvider ticketProvider)
        {
            this.ticketProvider = ticketProvider;
        }

        public string ProcessorName => "";
        public bool IsEnabled => true;

        public Task<string> CreateEvent(GenericEventRecord evt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The built-in provider has no external platform to push events to.");
        }

        public async Task<ReserveTicketResult> ReserveTicket(GenericEventRecord evt, GenericTicketClassRecord ticketClass, ONUser user, uint quantity, CancellationToken cancellationToken)
        {
            if (quantity == 0)
                return new ReserveTicketResult { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Quantity Must Be Greater Than Zero") };

            var now = DateTime.UtcNow;

            if (ticketClass.SaleStartOnUTC != null && ticketClass.SaleStartOnUTC.ToDateTime() > now)
                return new ReserveTicketResult { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Ticket Sales Have Not Started") };

            if (ticketClass.SaleEndOnUTC != null && ticketClass.SaleEndOnUTC.ToDateTime() < now)
                return new ReserveTicketResult { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Ticket Sales Have Ended") };

            var eventId = evt.EventID.ToGuid();
            var existingForEvent = await ticketProvider.GetAllByEventId(eventId).ToListAsync(cancellationToken);
            var activeForEvent = existingForEvent.Where(t => t.Status != GenericEventTicketStatus.TicketStatusCanceled).ToList();

            var activeForClass = activeForEvent.Where(t => t.TicketClassID == ticketClass.TicketClassID).ToList();
            if (ticketClass.AmountAvailable > 0 && activeForClass.Count + quantity > ticketClass.AmountAvailable)
                return new ReserveTicketResult { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonConflict, "Not Enough Tickets Available") };

            if (ticketClass.MaxTicketsPerUser > 0)
            {
                var userId = user.Id.ToString();
                var activeForUser = activeForClass.Count(t => t.UserID == userId);
                if (activeForUser + quantity > ticketClass.MaxTicketsPerUser)
                    return new ReserveTicketResult { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonConflict, "Exceeds Max Tickets Per User") };
            }

            if (ticketClass.CountTowardEventMax && evt.MaxTickets > 0 && activeForEvent.Count + quantity > evt.MaxTickets)
                return new ReserveTicketResult { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonConflict, "Event Is At Capacity") };

            var nowTs = Timestamp.FromDateTime(now);
            var result = new ReserveTicketResult { Success = true };

            for (var i = 0; i < quantity; i++)
            {
                var ticket = new GenericEventTicketRecord
                {
                    TicketID = Guid.NewGuid().ToString(),
                    EventID = evt.EventID,
                    TicketClassID = ticketClass.TicketClassID,
                    ProcessorName = evt.ProcessorName,
                    UserID = user.Id.ToString(),
                    Title = ticketClass.Name,
                    Status = GenericEventTicketStatus.TicketStatusAvailable,
                    CreatedOnUTC = nowTs,
                    ModifiedOnUTC = nowTs,
                    CreatedByID = user.Id.ToString(),
                    ModifiedByID = user.Id.ToString(),
                };

                await ticketProvider.Save(ticket);
                result.Tickets.Add(ticket);
            }

            return result;
        }

        public Task<bool> CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SyncResult> SyncTicket(string processorTicketId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<CreateEventResult> IGenericEventProvider.CreateEvent(GenericEventRecord evt, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        Task<CancelTicketResult> IGenericEventProvider.CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<SyncResult> SyncTicket(string processorEventID, string processorTicketID, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<EventSyncResult> SyncEvents(DateTime changedSince, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
