using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.Authorization.Payment;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Payment.Generic
{
    public class ProcessorPaymentRecord
    {
        public string ProcessorSubscriptionID = "";
        public string ProcessorPaymentID = "";
        public PaymentStatus Status;
        public uint AmountCents;
        public uint TaxCents;
        public uint TaxRateThousandPercents;
        public uint TotalCents;
        public DateTimeOffset CreatedOnUTC;
        public DateTimeOffset ModifiedOnUTC;
        public DateTimeOffset PaidOnUTC;
        public DateTimeOffset PaidThruUTC;

        public GenericPaymentRecord ToGenericPaymentRecord()
        {
            return new()
            {
                ProcessorPaymentID = ProcessorPaymentID,
                Status = Status,
                AmountCents = AmountCents,
                TaxCents = TaxCents,
                TaxRateThousandPercents = TaxRateThousandPercents,
                TotalCents = TotalCents,
                CreatedOnUTC = Timestamp.FromDateTimeOffset(CreatedOnUTC.UtcDateTime),
                ModifiedOnUTC = Timestamp.FromDateTimeOffset(ModifiedOnUTC.UtcDateTime),
                PaidOnUTC = Timestamp.FromDateTimeOffset(PaidOnUTC.UtcDateTime),
                PaidThruUTC = Timestamp.FromDateTimeOffset(PaidThruUTC.UtcDateTime),
            };
        }
    }
}
