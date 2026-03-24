using IT.WebServices.Fragments.Authorization.Payment.Tax;

namespace IT.WebServices.Authorization.Payment.Tax.Data
{
    public interface ISalesTaxRecordProvider
    {
        Task Delete(string countryCode, string postalCode);
        Task DeleteForCountry(string countryCode);
        Task<bool> Exists(string countryCode, string postalCode);
        Task<SalesTaxByPostalCodeRecord?> Get(string countryCode, string postalCode);
        IAsyncEnumerable<SalesTaxByPostalCodeRecord> GetAll();
        IAsyncEnumerable<SalesTaxByPostalCodeRecord> GetAllByCountryCode(string countryCode);
        Task Save(SalesTaxByPostalCodeRecord record);
    }
}
