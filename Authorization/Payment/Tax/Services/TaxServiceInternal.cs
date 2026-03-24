using IT.WebServices.Authorization.Payment.Tax.Data;
using IT.WebServices.Fragments.Authorization.Payment.Tax;

namespace IT.WebServices.Authorization.Payment.Tax.Services
{
    public class TaxServiceInternal
    {
        private readonly ISalesTaxRecordProvider dataProvider;

        public TaxServiceInternal(ISalesTaxRecordProvider dataProvider)
        {
            this.dataProvider = dataProvider;
        }

        public async Task<SalesTaxByPostalCodeRecord?> Get(string countryCode, string postalCode) => await dataProvider.Get(countryCode, postalCode);
    }
}
