using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Events.Generic;
using IT.WebServices.Fragments.Authorization.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Combined
{
    public class BuiltInGenericEventProvider : IGenericEventProvider
    {
        public string ProcessorName => "";
        public bool IsEnabled => true;

        public Task<ReserveTicketResult> ReserveTicket(GenericEventRecord evt, GenericTicketClassRecord ticketClass, ONUser user, uint quantity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SyncResult> SyncTicket(string processorTicketId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
