using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IT.WebServices.Fragments.AuditLog;

namespace IT.WebServices.AuditLog.Services.Data
{
    public interface IAuditLogDataProvider
    {
        Task<AuditLogEntry> Save(AuditLogEntry entry);
        Task<bool> Exists(Guid entryId);
        Task<AuditLogEntry> Get(Guid entryId);
        IAsyncEnumerable<AuditLogEntry> GetAll();
        Task<SearchEntriesResponse> Search(SearchEntriesRequest request);
    }
}
