using IT.WebServices.Fragments.Authorization.Payment.Tax;
using System.Data.Common;

namespace IT.WebServices.Authorization.Payment.Tax.Helpers
{
    internal static class SalesTaxByPostalCodeRecordHelper
    {
        public static SalesTaxByPostalCodeRecord ParseSalesTaxRecord(this DbDataReader rdr)
        {
            return new SalesTaxByPostalCodeRecord()
            {
                CountryCode = rdr["CountryCode"] as string ?? "",
                PostalCode = rdr["PostalCode"] as string ?? "",
                TaxRateThousandPercents = (uint)rdr["TaxRateThousandPercents"],
            };
        }

        public static SalesTaxTuple ToTuple(this SalesTaxByPostalCodeRecord record) => new(record);
    }

    public class SalesTaxTuple
    {
        public readonly string Tuple;

        public SalesTaxTuple(SalesTaxByPostalCodeRecord record)
        {
            Tuple = record.CountryCode + "-" + record.PostalCode;
        }

        public override bool Equals(object? obj) => Tuple.Equals(obj);

        public override int GetHashCode() => Tuple.GetHashCode();

        public override string ToString() => Tuple;
    }
}
