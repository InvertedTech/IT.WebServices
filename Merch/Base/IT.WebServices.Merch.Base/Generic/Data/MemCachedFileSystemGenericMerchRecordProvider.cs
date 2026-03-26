using IT.WebServices.Fragments.Generic;
using IT.WebServices.Fragments.Merch;
using System.Collections.Concurrent;

namespace IT.WebServices.Merch.Generic.Data
{
    public class MemCachedFileSystemGenericMerchRecordProvider : IGenericMerchRecordProvider
    {
        private readonly ConcurrentDictionary<Guid, GenericMerchRecord> cache = new();
        private readonly FileSystemGenericMerchRecordProvider dataProvider;

        public MemCachedFileSystemGenericMerchRecordProvider(FileSystemGenericMerchRecordProvider dataProvider)
        {
            this.dataProvider = dataProvider;
            LoadCache().Wait();
        }

        private async Task LoadCache()
        {
            await foreach(var r in dataProvider.GetAll())
            {
                cache.TryAdd(r.InternalProductId.ToGuid(), r);
            }
        }

        public Task Delete(Guid internalProductId)
        {
            if (cache.TryRemove(internalProductId, out var r))
                return dataProvider.Delete(internalProductId);

            return Task.FromResult(false);
        }

        public Task<bool> Exists(Guid internalProductId)
        {
            return Task.FromResult(cache.ContainsKey(internalProductId));
        }

        public IAsyncEnumerable<GenericMerchRecord> GetAll()
        {
            return cache.Values.Select(v => v.Clone()).ToAsyncEnumerable();
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

        public Task<GenericMerchRecord?> GetById(Guid internalProductId)
        {
            if (cache.TryGetValue(internalProductId, out var record))
                return Task.FromResult(record?.Clone());

            return Task.FromResult((GenericMerchRecord?)null);
        }

        public async Task Save(GenericMerchRecord record)
        {
            await dataProvider.Save(record);

            var record2 = await dataProvider.GetById(record.InternalProductId.ToGuid());

            if (record2 is null)
                return;

            cache.AddOrUpdate(record2.InternalProductId.ToGuid(), record2, (k,v) => record2);
        }
    }
}
