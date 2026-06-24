using IT.WebServices.Fragments.Authorization.Payment;
using System.Data.Common;

namespace IT.WebServices.Authorization.Payment.Generic.Data
{
    public static class ParserExtensions
    {
        public static GenericSubscriptionRecord? ParseSubscriptionRecord(this DbDataReader rdr)
        {
            var record = new GenericSubscriptionRecord()
            {
                InternalSubscriptionID = rdr["InternalSubscriptionID"] as string,
                UserID = rdr["UserID"] as string,
                ProcessorName = rdr["ProcessorName"] as string,
                ProcessorCustomerID = rdr["ProcessorCustomerID"] as string,
                ProcessorSubscriptionID = rdr["ProcessorSubscriptionID"] as string,
                Status = (SubscriptionStatus)(byte)rdr["Status"],
                AmountCents = (uint)rdr["AmountCents"],
                TaxCents = (uint)rdr["TaxCents"],
                TaxRateThousandPercents = (uint)rdr["TaxRateThousandPercents"],
                TotalCents = (uint)rdr["TotalCents"],
                CreatedBy = rdr["CreatedBy"] as string ?? "",
                ModifiedBy = rdr["ModifiedBy"] as string ?? "",
                CanceledBy = rdr["CanceledBy"] as string ?? "",
                CardExpYear = (uint)rdr["CardExpYear"],
                CardExpMonth = (uint)rdr["CardExpMonth"],
                CardLast4 = rdr["CardLast4"] as string ?? "",
                OldSubscriptionID = rdr["OldSubscriptionID"] as string ?? "",
            };

            DateTime d;
            if (!(rdr["CreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc);
                record.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["ModifiedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["ModifiedOnUTC"], DateTimeKind.Utc);
                record.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["CanceledOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CanceledOnUTC"], DateTimeKind.Utc);
                record.CanceledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            return record;
        }

        public static GenericPaymentRecord? ParsePaymentRecord(this DbDataReader rdr)
        {
            var record = new GenericPaymentRecord()
            {
                InternalPaymentID = rdr["InternalPaymentID"] as string,
                InternalSubscriptionID = rdr["InternalSubscriptionID"] as string,
                UserID = rdr["UserID"] as string,
                ProcessorPaymentID = rdr["ProcessorPaymentID"] as string,
                Status = (PaymentStatus)(byte)rdr["Status"],
                AmountCents = (uint)rdr["AmountCents"],
                TaxCents = (uint)rdr["TaxCents"],
                TaxRateThousandPercents = (uint)rdr["TaxRateThousandPercents"],
                TotalCents = (uint)rdr["TotalCents"],
                CreatedBy = rdr["CreatedBy"] as string ?? "",
                ModifiedBy = rdr["ModifiedBy"] as string ?? "",
                OldPaymentID = rdr["OldPaymentID"] as string ?? "",
            };

            DateTime d;
            if (!(rdr["CreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc);
                record.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["ModifiedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["ModifiedOnUTC"], DateTimeKind.Utc);
                record.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["PaidOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["PaidOnUTC"], DateTimeKind.Utc);
                record.PaidOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["PaidThruUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["PaidThruUTC"], DateTimeKind.Utc);
                record.PaidThruUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            return record;
        }
    }
}
