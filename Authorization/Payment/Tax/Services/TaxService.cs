using Grpc.Core;
using IT.WebServices.Authorization.Payment.Tax.Data;
using IT.WebServices.Fragments.Authorization.Payment.Tax;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Authorization.Payment.Tax.Services
{
    public class TaxService : TaxInterface.TaxInterfaceBase
    {
        private readonly ILogger log;
        private readonly ISalesTaxRecordProvider dataProvider;

        public TaxService(ILogger<TaxService> log, ISalesTaxRecordProvider dataProvider)
        {
            this.log = log;
            this.dataProvider = dataProvider;
        }

        [AllowAnonymous]
        public override async Task<GetSalesTaxRecordByPostalCodeResponse> GetSalesTaxRecordByPostalCode(GetSalesTaxRecordByPostalCodeRequest request, ServerCallContext context)
        {
            return new()
            {
                Record = await dataProvider.Get(request.CountryCode, request.PostalCode),
            };
        }
    }
}
