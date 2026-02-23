using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using IT.WebServices.AuditLog.Services.Data;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.AuditLog;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.AuditLog.Services
{
    [Authorize]
    public class AuditLogService : AuditLogInterface.AuditLogInterfaceBase
    {
        private readonly ILogger<AuditLogService> _logger;
        private readonly IAuditLogDataProvider _db;
        private readonly OfflineHelper _offlineHelper;

        public AuditLogService(ILogger<AuditLogService> logger, IAuditLogDataProvider db, OfflineHelper offlineHelper)
        {
            _logger = logger;
            _db = db;
            _offlineHelper = offlineHelper;
        }

        public override async Task<LogEntryResponse> LogEntry(LogEntryRequest request, ServerCallContext context)
        {
            if (_offlineHelper.IsOffline)
            {
                return new LogEntryResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonDeliveryFailed,
                        Message = "The service is currently unavailable. Please try again later."
                    }
                };
            }

            try
            {
                var entry = await _db.Save(request.Entry);
                return new LogEntryResponse
                {
                    EntryId = entry.EntryID
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry");
                return new LogEntryResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonProviderError,
                        Message = "Failed to log the audit entry. Please try again later."
                    }
                };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<SearchEntriesResponse> SearchEntries(SearchEntriesRequest request, ServerCallContext context)
        {
            if (_offlineHelper.IsOffline)
            {
                return new SearchEntriesResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonDeliveryFailed,
                        Message = "The service is currently unavailable. Please try again later."
                    }
                };
            }

            try
            {
                List<AuditLogEntry> entries = new();
                var res = new SearchEntriesResponse();

                await foreach (var entry in _db.GetAll())
                {
                    entries.Add(entry);
                }

                res.Entries.AddRange(entries.OrderByDescending(r => r.CreatedOnUTC));
                res.PageTotalItems = (uint)res.Entries.Count();

                if (request.PageSize > 0)
                {
                    res.PageOffsetStart = request.PageOffset;

                    var page = res.Entries.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                    res.Entries.Clear();
                    res.Entries.AddRange(page);
                }

                res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Entries.Count;
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search audit log entries");
                return new SearchEntriesResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonProviderError,
                        Message = "Failed to search the audit log entries. Please try again later."
                    }
                };
            }
        }
    }
}
