using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.AuditLog;
using IT.WebServices.Fragments.Careers;
using IT.WebServices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Careers.Data
{
    public class FileSystemCareersDataProvider : ICareersDataProvider
    {
        private readonly ILogger<FileSystemCareersDataProvider> logger;
        private readonly FileInfo dataFile;

        public FileSystemCareersDataProvider(IOptions<AppSettings> settings, ILogger<FileSystemCareersDataProvider> logger)
        {
            this.logger = logger;

            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataFile = new FileInfo(Path.Combine(root.FullName, "jobs"));
        }

        public async Task<bool> Exists(Guid entryId)
        {
            return await Get(entryId) != null;
        }

        public async Task<CareerRecord> Get(Guid entryId)
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
                    var entry = CareerRecord.Parser.ParseFrom(Convert.FromBase64String(lines[i]));
                    if (Guid.TryParse(entry.CareerId, out var parsedId) && parsedId == entryId)
                        return entry;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping unreadable audit log entry line {LineNumber}", i + 1);
                }
            }

            return null;
        }

        public async IAsyncEnumerable<CareerRecord> GetAll()
        {
            if (!dataFile.Exists || dataFile.Length == 0)
                yield break;

            var lines = await File.ReadAllLinesAsync(dataFile.FullName);
            foreach (var line in lines)
            {
                CareerRecord entry;
                try
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    entry = CareerRecord.Parser.ParseFrom(Convert.FromBase64String(line));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping unreadable audit log entry line");
                    continue;
                }

                yield return entry;
            }
        }

        public async Task<CareerRecord> Save(CareerRecord entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            bool isNew = !Guid.TryParse(entry.CareerId, out var id) || id == Guid.Empty;
            if (isNew)
            {
                id = Guid.NewGuid();
                entry.CareerId = id.ToString();
            }

            if (entry.CreatedOnUTC == null || (entry.CreatedOnUTC.Seconds == 0 && entry.CreatedOnUTC.Nanos == 0))
                entry.CreatedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);

            var newLine = Convert.ToBase64String(entry.ToByteArray());

            if (isNew)
            {
                await File.AppendAllTextAsync(dataFile.FullName, newLine + "\n");

                dataFile.Refresh();
                return entry;
            }
            var lines = await File.ReadAllLinesAsync(dataFile.FullName);
            bool replaced = false;

            for (int i = 0; i < lines.Length; i++)
            {
               var existing = CareerRecord.Parser.ParseFrom(Convert.FromBase64String(lines[i]));
                if (existing.CareerId == entry.CareerId)
                {
                    lines[i] = newLine;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
                lines = lines.Append(newLine).ToArray();

            await File.WriteAllLinesAsync(dataFile.FullName, lines);

            dataFile.Refresh();
            return entry;
        }
    }
}
