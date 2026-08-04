using IT.WebServices.Fragments.Authorization.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Events.Generic.Data
{
    public interface IGenericEventTicketRecordProvider
    {
        IAsyncEnumerable<GenericEventTicketRecord> GetAll();
        IAsyncEnumerable<GenericEventTicketRecord> GetAllByEventId(Guid eventId);
        IAsyncEnumerable<GenericEventTicketRecord> GetAllByUserId(Guid userId);
        Task Save(GenericEventTicketRecord record);
    }
}
