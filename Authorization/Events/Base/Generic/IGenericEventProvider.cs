using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Authorization.Events;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Generic
{
    public interface IGenericEventProvider
    {
        string ProcessorName { get; }
        bool IsEnabled { get; }

        // Outbound push, used by AdminCreateEvent's SyncToEventbrite flag. Returns
        // the provider's event ID on success. Not called for the built-in provider.
        Task<string> CreateEvent(GenericEventRecord evt, CancellationToken cancellationToken);

        Task<ReserveTicketResult> ReserveTicket(GenericEventRecord evt, GenericTicketClassRecord ticketClass, ONUser user, uint quantity, CancellationToken cancellationToken);
        Task<bool> CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken cancellationToken);
        Task<SyncResult> SyncTicket(string processorTicketId, CancellationToken cancellationToken);
    }
}
