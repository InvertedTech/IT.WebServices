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
    public class FileSystemEventRecordProvider : IGenericEventRecordProvider
    {
        private readonly DirectoryInfo dataDir;

        public FileSystemEventRecordProvider(IOptions<AppSettings> settings)
        {
            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataDir = root.CreateSubdirectory(EventConstants.EVENT_DIR_NAME).CreateSubdirectory("events");
        }

        public Task Delete(Guid eventId)
        {
            var fi = GetDataFilePath(eventId);
            if (fi.Exists)
                fi.Delete();

            return Task.CompletedTask;
        }

        public Task<bool> Exists(Guid eventId)
        {
            var fi = GetDataFilePath(eventId);
            return Task.FromResult(fi.Exists);
        }

        public async IAsyncEnumerable<GenericEventRecord> GetAll()
        {
            foreach (var fi in GetAllDataFiles())
                yield return GenericEventRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fi.FullName));
        }

        public async Task<GenericEventRecord?> GetById(Guid eventId)
        {
            var fi = GetDataFilePath(eventId);
            if (!fi.Exists)
                return null;

            return GenericEventRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fi.FullName));
        }

        public async Task<GenericEventRecord?> GetByProcessorId(string processorEventId)
        {
            if (string.IsNullOrEmpty(processorEventId))
                return null;

            await foreach (var record in GetAll())
                if (record.ProcessorEventID == processorEventId)
                    return record;

            return null;
        }

        public async Task Save(GenericEventRecord record)
        {
            var id = record.EventID.ToGuid();
            var fi = GetDataFilePath(id);
            await File.WriteAllBytesAsync(fi.FullName, record.ToByteArray());
        }

        private IEnumerable<FileInfo> GetAllDataFiles() => dataDir.EnumerateFiles("*", SearchOption.AllDirectories);

        private FileInfo GetDataFilePath(Guid eventId) => dataDir.CreateGuidFileInfo(eventId);
    }
}
