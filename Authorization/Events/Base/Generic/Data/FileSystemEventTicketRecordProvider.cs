using Google.Protobuf;
using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using IT.WebServices.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Generic.Data
{
    public class FileSystemEventTicketRecordProvider : IGenericEventTicketRecordProvider
    {
        private readonly DirectoryInfo dataDir;

        public FileSystemEventTicketRecordProvider(IOptions<AppSettings> settings)
        {
            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataDir = root.CreateSubdirectory(EventConstants.EVENT_DIR_NAME).CreateSubdirectory("tickets");
        }

        public async IAsyncEnumerable<GenericEventTicketRecord> GetAll()
        {
            foreach (var fi in dataDir.EnumerateFiles("*", SearchOption.AllDirectories))
                yield return GenericEventTicketRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fi.FullName));
        }

        public async IAsyncEnumerable<GenericEventTicketRecord> GetAllByEventId(Guid eventId)
        {
            var dir = GetEventDirPath(eventId);
            if (!dir.Exists)
                yield break;

            foreach (var fi in dir.EnumerateFiles())
                yield return GenericEventTicketRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fi.FullName));
        }

        public async IAsyncEnumerable<GenericEventTicketRecord> GetAllByUserId(Guid userId)
        {
            await foreach (var record in GetAll())
                if (record.UserID.ToGuid() == userId)
                    yield return record;
        }

        public async Task Save(GenericEventTicketRecord record)
        {
            var fi = GetDataFilePath(record.EventID.ToGuid(), record.TicketID.ToGuid());
            await File.WriteAllBytesAsync(fi.FullName, record.ToByteArray());
        }

        private DirectoryInfo GetEventDirPath(Guid eventId) => dataDir.CreateGuidDirectory(eventId);

        private FileInfo GetDataFilePath(Guid eventId, Guid ticketId)
        {
            var dir = GetEventDirPath(eventId);
            return new FileInfo(Path.Combine(dir.FullName, ticketId.ToString()));
        }
    }
}
