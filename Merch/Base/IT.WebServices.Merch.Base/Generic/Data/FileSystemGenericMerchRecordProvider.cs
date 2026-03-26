using Google.Protobuf;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Models;
using Microsoft.Extensions.Options;

namespace IT.WebServices.Merch.Generic.Data
{
    public class FileSystemGenericMerchRecordProvider : IGenericMerchRecordProvider
    {
        private readonly DirectoryInfo dataDir;

        public FileSystemGenericMerchRecordProvider(IOptions<AppSettings> settings)
        {
            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataDir = root.CreateSubdirectory("merch").CreateSubdirectory("generic");
        }

        public Task Delete(Guid internalProductId)
        {
            var fi = GetDataFilePath(internalProductId);
            if (fi.Exists)
                fi.Delete();

            return Task.CompletedTask;
        }

        public Task<bool> Exists(Guid internalProductId)
        {
            var fi = GetDataFilePath(internalProductId);
            return Task.FromResult(fi.Exists);
        }

        public async IAsyncEnumerable<GenericMerchRecord> GetAll()
        {
            await foreach (var id in GetAllProductIds())
            {
                var record = await ReadFromFile(id);
                if (record != null)
                    yield return record;
            }
        }

        public async IAsyncEnumerable<GenericMerchRecord> GetAllByStoreId(Guid internalStoreId)
        {
            var idStr = internalStoreId.ToString();

            await foreach (var record in GetAll())
            {
                if (record.InternalStoreId == idStr)
                    yield return record;
            }
        }

        public async IAsyncEnumerable<Guid> GetAllProductIds()
        {
            foreach (var fi in dataDir.EnumerateFiles("*.*", SearchOption.AllDirectories))
            {
                var internalProductId = fi.Name.ToGuid();

                if (internalProductId == Guid.Empty) continue;

                yield return internalProductId;
            }
        }

        public Task<GenericMerchRecord?> GetById(Guid internalProductId)
        {
            var fi = GetDataFilePath(internalProductId);
            return ReadFromFile(fi);
        }

        public async Task Save(GenericMerchRecord rec)
        {
            var internalProductId = rec.InternalProductId.ToGuid();
            var fi = GetDataFilePath(internalProductId);
            await File.WriteAllTextAsync(fi.FullName, Convert.ToBase64String(rec.ToByteArray()));
        }

        private DirectoryInfo GetDataDirPath(Guid internalProductId)
        {
            var internalProductIdStr = internalProductId.ToString();
            var dir = dataDir.CreateSubdirectory(internalProductIdStr.Substring(0, 2)).CreateSubdirectory(internalProductIdStr.Substring(2, 2));
            return dir;
        }

        private FileInfo GetDataFilePath(Guid internalProductId)
        {
            var internalProductIdStr = internalProductId.ToString();
            var dir = GetDataDirPath(internalProductId);
            return new FileInfo(dir.FullName + "/" + internalProductIdStr);
        }

        private Task<GenericMerchRecord?> ReadFromFile(Guid id) => ReadFromFile(GetDataFilePath(id));

        private async Task<GenericMerchRecord?> ReadFromFile(FileInfo fi)
        {
            if (!fi.Exists)
                return null;

            var str = await File.ReadAllTextAsync(fi.FullName);
            if (string.IsNullOrWhiteSpace(str))
                return null;

            return GenericMerchRecord.Parser.ParseFrom(Convert.FromBase64String(str));
        }
    }
}
