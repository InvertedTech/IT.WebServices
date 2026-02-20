using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.AuditLog;
using IT.WebServices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.AuditLog.Services.Data
{
    public class FileSystemAuditLogDataProvider : IAuditLogDataProvider
    {
        private const string ResourceTypeKey = "resource_type";
        private const string ResourceIdKey = "resource_id";
        private const string RequestIdKey = "request_id";
        private const string SuccessKey = "success";

        private readonly FileInfo dataFile;
        private readonly ILogger logger;

        public FileSystemAuditLogDataProvider(IOptions<AppSettings> settings, ILogger<FileSystemAuditLogDataProvider> logger)
        {
            this.logger = logger;

            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataFile = new FileInfo(Path.Combine(root.FullName, "auditlog"));
        }

        public async Task<AuditLogEntry> Save(AuditLogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!Guid.TryParse(entry.EntryID, out var id) || id == Guid.Empty)
            {
                id = Guid.NewGuid();
                entry.EntryID = id.ToString();
            }

            if (entry.CreatedOnUTC == null || (entry.CreatedOnUTC.Seconds == 0 && entry.CreatedOnUTC.Nanos == 0))
                entry.CreatedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);

            var line = Convert.ToBase64String(entry.ToByteArray()) + "\n";
            await File.AppendAllTextAsync(dataFile.FullName, line);
            dataFile.Refresh();

            return entry;
        }

        public async Task<bool> Exists(Guid entryId)
        {
            return await Get(entryId) != null;
        }

        public async Task<AuditLogEntry> Get(Guid entryId)
        {
            if (entryId == Guid.Empty)
                return null;

            if (!dataFile.Exists || dataFile.Length == 0)
                return null;

            var lines = await File.ReadAllLinesAsync(dataFile.FullName);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                try
                {
                    var entry = AuditLogEntry.Parser.ParseFrom(Convert.FromBase64String(lines[i]));
                    if (Guid.TryParse(entry.EntryID, out var parsedId) && parsedId == entryId)
                        return entry;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping unreadable audit log entry line {LineNumber}", i + 1);
                }
            }

            return null;
        }

        public async IAsyncEnumerable<AuditLogEntry> GetAll()
        {
            if (!dataFile.Exists || dataFile.Length == 0)
                yield break;

            var lines = await File.ReadAllLinesAsync(dataFile.FullName);
            foreach (var line in lines)
            {
                AuditLogEntry entry;
                try
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    entry = AuditLogEntry.Parser.ParseFrom(Convert.FromBase64String(line));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping unreadable audit log entry line");
                    continue;
                }

                yield return entry;
            }
        }

        public async Task<SearchEntriesResponse> Search(SearchEntriesRequest request)
        {
            request ??= new SearchEntriesRequest();

            var records = new List<AuditLogEntry>();
            await foreach (var entry in GetAll())
            {
                if (!Matches(entry, request))
                    continue;

                records.Add(entry);
            }

            records = records
                .OrderByDescending(r => r.CreatedOnUTC?.Seconds ?? 0)
                .ThenByDescending(r => r.CreatedOnUTC?.Nanos ?? 0)
                .ToList();

            var pageSize = request.PageSize == 0 ? 50 : (int)request.PageSize;
            var pageOffset = (int)request.PageOffset;
            var page = records.Skip(pageOffset).Take(pageSize).ToArray();

            return new SearchEntriesResponse()
            {
                Entries = { page },
                PageOffsetStart = (uint)(records.Count == 0 ? 0 : pageOffset),
                PageOffsetEnd = (uint)(records.Count == 0 ? 0 : pageOffset + page.Length),
                PageTotalItems = (uint)records.Count
            };
        }

        private static bool Matches(AuditLogEntry entry, SearchEntriesRequest request)
        {
            if (entry == null)
                return false;

            if (!string.IsNullOrWhiteSpace(request.ActorUserID) &&
                !string.Equals(entry.Actor?.UserID, request.ActorUserID, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(request.TargetUserID))
            {
                var hasTargetUser = entry.Targets.Any(t => t.Type == TargetType.TargetUser &&
                    string.Equals(t.TargetID, request.TargetUserID, StringComparison.OrdinalIgnoreCase));
                if (!hasTargetUser)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(request.ActionPrefix))
            {
                var actionName = entry.Action.ToString();
                if (!actionName.StartsWith(request.ActionPrefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(request.ResourceType) &&
                !string.Equals(GetMetadataValue(entry, ResourceTypeKey), request.ResourceType, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(request.ResourceID) &&
                !string.Equals(GetMetadataValue(entry, ResourceIdKey), request.ResourceID, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(request.RequestID) &&
                !string.Equals(GetMetadataValue(entry, RequestIdKey), request.RequestID, StringComparison.OrdinalIgnoreCase))
                return false;

            if (request.SuccessOnly)
            {
                var successValue = GetMetadataValue(entry, SuccessKey);
                if (!string.Equals(successValue, "true", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            var createdOn = entry.CreatedOnUTC?.ToDateTime() ?? DateTime.MinValue;

            if (request.OccurredAfterUTC != null &&
                (request.OccurredAfterUTC.Seconds != 0 || request.OccurredAfterUTC.Nanos != 0) &&
                createdOn < request.OccurredAfterUTC.ToDateTime())
                return false;

            if (request.OccurredBeforeUTC != null &&
                (request.OccurredBeforeUTC.Seconds != 0 || request.OccurredBeforeUTC.Nanos != 0) &&
                createdOn > request.OccurredBeforeUTC.ToDateTime())
                return false;

            return true;
        }

        private static string GetMetadataValue(AuditLogEntry entry, string key)
        {
            if (entry.Metadata != null && entry.Metadata.TryGetValue(key, out var value))
                return value;

            return string.Empty;
        }
    }
}
