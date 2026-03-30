using IT.WebServices.Fragments.Merch;

namespace IT.WebServices.Merch.Generic.Data
{
    public interface IGenericMerchRecordProvider
    {
        Task Delete(Guid internalProductId);
        Task<bool> Exists(Guid internalProductId);
        IAsyncEnumerable<GenericMerchRecord> GetAll();
        IAsyncEnumerable<GenericMerchRecord> GetAllByStoreId(Guid internalStoreId);
        Task<GenericMerchRecord?> GetById(Guid internalProductId);
        Task<GenericMerchRecord?> GetByProcessorProductId(string processorProductId);
        Task Save(GenericMerchRecord record);
    }
}
