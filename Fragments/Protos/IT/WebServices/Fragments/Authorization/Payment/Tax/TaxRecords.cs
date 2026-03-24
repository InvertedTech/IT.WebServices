using System;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Authorization.Payment.Tax
{
    public sealed partial class SalesTaxByPostalCodeRecord : pb::IMessage<SalesTaxByPostalCodeRecord>
    {
        public decimal TaxRateAsDecimal => TaxRateThousandPercents / 1000.0M / 100.0M;

        public uint CalculateTax(uint subAmountInCents)
        {
            var taxAmountInCents = subAmountInCents * TaxRateAsDecimal;
            var rounded = Math.Round(taxAmountInCents);

            return (uint)rounded;
        }
    }
}
