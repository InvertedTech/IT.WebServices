using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IT.WebServices.Authorization.Events.Data
{
    public class FileSystemTicketDataProvider : ITicketDataProvider
    {
        private readonly ILogger _logger;
        private readonly DirectoryInfo dataDir;

        public FileSystemTicketDataProvider(
            IOptions<AppSettings> settings,
            ILogger<FileSystemTicketDataProvider> logger
        )
        {
            _logger = logger;

            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataDir = root.CreateSubdirectory("event").CreateSubdirectory("tickets");
        }

        public async Task<bool> Create(EventTicketRecord record)
        {
            Guid.TryParse(record.TicketId, out var ticketId);
            if (ticketId == Guid.Empty)
                return false;

            Guid.TryParse(record.Public.EventId, out var eventId);
            if (eventId == Guid.Empty)
                return false;

            var file = GetDataFilePath(eventId, ticketId);
            if (file.Exists)
                return false;

            await Save(record);
            return true;
        }

        public async Task<bool> Create(List<EventTicketRecord> records)
        {
            bool allSucceeded = true;
            foreach (var record in records)
            {
                var success = await Create(record);
                if (!success)
                {
                    allSucceeded = false;
                    break;
                }
            }
            return allSucceeded;
        }

        public async IAsyncEnumerable<EventTicketRecord> GetAllByEvent(Guid eventId)
        {
            var eventFolder = new DirectoryInfo(Path.Combine(dataDir.FullName, eventId.ToString()));
            if (!eventFolder.Exists)
                yield break;

            foreach (var file in eventFolder.EnumerateFiles("*"))
            {
                yield return EventTicketRecord.Parser.ParseFrom(
                    await File.ReadAllBytesAsync(file.FullName)
                );
            }
        }

        public async Task<EventTicketRecord?> GetById(Guid ticketId, Guid eventId)
        {
            var file = dataDir
                .EnumerateFiles(eventId.ToString(), SearchOption.AllDirectories)
                .Where(f => f.Name == ticketId.ToString())
                .FirstOrDefault();

            if (file == null)
                return null;

            return EventTicketRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(file.FullName));
        }

        private async Task Save(EventTicketRecord record)
        {
            Guid.TryParse(record.TicketId, out var ticketId);
            if (ticketId == Guid.Empty)
                throw new ArgumentException("Invalid TicketId");

            Guid.TryParse(record.Public.EventId, out var eventId);
            if (eventId == Guid.Empty)
                throw new ArgumentException("Invalid EventId");

            var file = GetDataFilePath(eventId, ticketId);
            await File.WriteAllBytesAsync(file.FullName, record.ToByteArray());
        }

        public async IAsyncEnumerable<EventTicketRecord> GetAllByUser(Guid userId)
        {
            foreach (var file in GetAllDataFiles())
            {
                EventTicketRecord record;
                try
                {
                    record = EventTicketRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(file.FullName));
                }
                catch
                {
                    continue;
                }

                if (Guid.TryParse(record.Private?.UserId, out var ownerId) && ownerId == userId)
                {
                    yield return record;
                }
            }
        }

        public async IAsyncEnumerable<EventTicketRecord> GetAllByUserAndEvent(Guid userId, Guid eventId)
        {
            var eventFolder = new DirectoryInfo(Path.Combine(dataDir.FullName, eventId.ToString()));
            if (!eventFolder.Exists)
                yield break;
            foreach (var file in eventFolder.EnumerateFiles("*"))
            {
                EventTicketRecord record;
                try
                {
                    record = EventTicketRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(file.FullName));
                }
                catch
                {
                    continue;
                }
                if (Guid.TryParse(record.Private?.UserId, out var ownerId) && ownerId == userId)
                {
                    yield return record;
                }
            }
        }

        public async Task<bool> Update(EventTicketRecord record)
        {
            // Validate TicketId and EventId
            if (!Guid.TryParse(record.TicketId, out var ticketId) || ticketId == Guid.Empty)
                return false;

            if (!Guid.TryParse(record.Public?.EventId, out var eventId) || eventId == Guid.Empty)
                return false;

            var file = GetDataFilePath(eventId, ticketId);

            // Only update if the ticket file exists
            if (!file.Exists)
                return false;

            await Save(record);
            return true;
        }

        private IEnumerable<FileInfo> GetAllDataFiles()
        {
            return dataDir.EnumerateFiles("*", SearchOption.AllDirectories);
        }

        private FileInfo GetDataFilePath(Guid eventId, Guid ticketId)
        {
            var eventFolder = dataDir.CreateSubdirectory(eventId.ToString());
            return new FileInfo(Path.Combine(eventFolder.FullName, ticketId.ToString()));
        }
    }
}
