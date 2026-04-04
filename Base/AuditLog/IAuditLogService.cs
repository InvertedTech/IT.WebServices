using IT.WebServices.Fragments.AuditLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.AuditLog
{
    public interface IAuditLogService
    {
        Task<LogEntryResponse> LogEvent(AuditLogEntry entry);
        Task TryLogEvent(AuditLogEntry entry);
        Task TryLogEvent(Func<AuditLogEntry> entryFactory);
    }
}
