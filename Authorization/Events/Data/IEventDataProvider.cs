using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IT.WebServices.Fragments.Authorization.Events;

namespace IT.WebServices.Authorization.Events.Data
{
    public interface IEventDataProvider
    {
        Task<bool> Create(EventRecord record);
        Task<bool> CreateRecurring(IEnumerable<EventRecord> records);
        Task<EventRecord?> GetById(Guid id);
        IAsyncEnumerable<EventRecord> GetEvents();
        Task<bool> Update(EventRecord record);
        Task<bool> UpdateRecurring(IEnumerable<EventRecord> records);
        Task<bool> Exists(Guid eventId);
    }
}
