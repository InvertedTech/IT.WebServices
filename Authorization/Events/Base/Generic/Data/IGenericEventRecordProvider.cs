using IT.WebServices.Fragments.Authorization.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Events.Generic.Data
{
    public interface IGenericEventRecordProvider
    {
        Task Delete(Guid eventId);
        Task<bool> Exists(Guid eventId);
        IAsyncEnumerable<GenericEventRecord> GetAll();
        Task<GenericEventRecord?> GetById(Guid eventId);
        Task<GenericEventRecord?> GetByProcessorId(string processorEventId);
        Task Save(GenericEventRecord record);
    }
}
