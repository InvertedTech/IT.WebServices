using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using IT.WebServices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IT.WebServices.Authorization.Events.Data
{
    public class FileSystemEventDataProvider : IEventDataProvider
    {
        private readonly ILogger<FileSystemEventDataProvider> _logger;
        private readonly DirectoryInfo _dataDir;

        public FileSystemEventDataProvider(
            ILogger<FileSystemEventDataProvider> logger,
            IOptions<AppSettings> settings
        )
        {
            _logger = logger;
            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            _dataDir = root.CreateSubdirectory("event").CreateSubdirectory("events");
        }

        public async Task<bool> Create(EventRecord record)
        {
            try
            {
                var file = GetDataFilePath(record.EventId.ToGuid());
                if (file.Exists)
                    return false; // File already exists

                await Save(record);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create event {EventId}", record.EventId);
                return false;
            }
        }

        public async Task<bool> CreateRecurring(
            IEnumerable<EventRecord> records
        )
        {
            if (records == null || !records.Any())
                return false;

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.EventId))
                    return false;

                // Sanity check: make sure all records are of recurring type
                if (record.OneOfType != EventRecordOneOfType.EventOneOfRecurring)
                    return false;

                var file = GetDataFilePath(record.EventId.ToGuid());
                if (file.Exists)
                {
                    _logger.LogWarning(
                        "Recurring event with ID {EventId} already exists at {Path}.",
                        record.EventId,
                        file.FullName
                    );
                    return false;
                }

                try
                {
                    await Save(record);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to save recurring event {EventId}",
                        record.EventId
                    );
                    return false;
                }
            }

            return true;
        }

        public async Task<EventRecord?> GetById(Guid id)
        {
            try
            {
                var fd = GetDataFilePath(id);
                if (!fd.Exists)
                    return null;

                var record = EventRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fd.FullName));
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get event {EventId}", id);
                return null;
            }
        }

        public async IAsyncEnumerable<EventRecord> GetEvents()
        {
            foreach (var file in _dataDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (file.Length == 0)
                    continue;

                yield return EventRecord.Parser.ParseFrom(
                    await File.ReadAllBytesAsync(file.FullName)
                );
            }
        }

        public async Task<bool> Update(EventRecord record)
        {
            try
            {
                await Save(record);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update event {EventId}", record.EventId);
                return false;
            }
        }

        public async Task<bool> UpdateRecurring(
            IEnumerable<EventRecord> records
        )
        {
            if (records == null || !records.Any())
                return false;

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.EventId))
                    return false;

                if (record.OneOfType != EventRecordOneOfType.EventOneOfRecurring)
                    return false;

                try
                {
                    await Save(record);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to save recurring event {EventId} during update.",
                        record.EventId
                    );
                    return false;
                }
            }

            return true;
        }

        public Task<bool> Exists(Guid eventId)
        {
            var file = GetDataFilePath(eventId);
            return Task.FromResult(file.Exists);
        }

        private async Task Save(EventRecord record)
        {
            var id = record.EventId.ToGuid();
            var fd = GetDataFilePath(id);
            await File.WriteAllBytesAsync(fd.FullName, record.ToByteArray());
        }

        private FileInfo GetDataFilePath(Guid eventId)
        {
            return _dataDir.CreateGuidFileInfo(eventId);
        }
    }
}
