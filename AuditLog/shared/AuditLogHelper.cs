using System;
using System.Threading.Tasks;
using Grpc.Core;
using IT.WebServices.Fragments.AuditLog;
using IT.WebServices.Settings;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.AuditLog
{
    public class AuditLogHelper
    {
        private readonly ServiceNameHelper _serviceNameHelper;

        public AuditLogHelper(ServiceNameHelper serviceNameHelper)
        {
            _serviceNameHelper = serviceNameHelper;
        }

        public async Task TryLogEvent(
                AuditLogEntry entry,
                ILogger logger
            )
        {
            try
            {
                await LogEvent(entry);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to log audit event");
            }
        }

        public async Task TryLogEvent(
        Func<AuditLogEntry> entryFactory,
        ILogger logger
    )
        {
            try
            {
                await LogEvent(entryFactory());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to log audit event");
            }
        }

        public async Task<LogEntryResponse> LogEvent(AuditLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var client = new AuditLogInterface.AuditLogInterfaceClient(
                _serviceNameHelper.AuditLogServiceChannel
            );

            return await client.LogEntryAsync(
                new LogEntryRequest { Entry = entry },
                GetMetadata()
            );
        }

        private Metadata GetMetadata()
        {
            var data = new Metadata();
            data.Add("Authorization", "Bearer " + _serviceNameHelper.ServiceToken);
            return data;
        }
    }
}
