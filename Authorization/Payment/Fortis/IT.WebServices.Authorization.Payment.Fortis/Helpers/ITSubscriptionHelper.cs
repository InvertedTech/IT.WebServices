using FortisAPI.Standard.Models;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.Authorization.Payment;

namespace IT.WebServices.Authorization.Payment.Fortis.Helpers
{
    internal static class ITSubscriptionHelper
    {
        public static GenericSubscriptionRecord ToSubscriptionRecord(this Data9 fRec)
        {
            return new()
            {
                ProcessorName = PaymentConstants.PROCESSOR_NAME_FORTIS,
                ProcessorSubscriptionID = fRec.Id,
                ProcessorCustomerID = fRec.ContactId,
                CreatedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(fRec.CreatedTs).UtcDateTime),
                Status = ConvertStatus(fRec.Status),
                AmountCents = (uint)fRec.TransactionAmount,
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = (uint)fRec.TransactionAmount,
            };
        }

        public static GenericSubscriptionRecord ToSubscriptionRecord(this List6 fRec)
        {
            return new()
            {
                ProcessorName = PaymentConstants.PROCESSOR_NAME_FORTIS,
                ProcessorSubscriptionID = fRec.Id ?? "",
                ProcessorCustomerID = fRec.ContactId ?? "",
                CreatedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(fRec.CreatedTs).UtcDateTime),
                Status = ConvertStatus(fRec.Status),
                AmountCents = (uint)fRec.TransactionAmount,
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = (uint)fRec.TransactionAmount,
            };
        }

        public static GenericSubscriptionFullRecord ToSubscriptionFullRecord(this List6 fRec)
        {
            var record = new GenericSubscriptionFullRecord()
            {
                SubscriptionRecord = fRec.ToSubscriptionRecord(),
            };

            foreach (var t in fRec.Transactions)
                record.Payments.Add(t.ToGenericPaymentRecord());

            return record;
        }

        public static GenericSubscriptionRecord? ToSubscriptionRecord(this ResponseRecurring fRec) => fRec?.Data?.ToSubscriptionRecord();
        public static List<GenericSubscriptionRecord> ToSubscriptionRecords(this ResponseRecurringsCollection fRec)
        {
            return fRec?.List
                        .Select(r => r?.ToSubscriptionRecord())
                        .Where(r => r is not null)
                        .Select(r => r!)
                        .ToList() ?? new List<GenericSubscriptionRecord>();
        }

        private static SubscriptionStatus ConvertStatus(StatusEnum? status)
        {
            switch (status)
            {
                case StatusEnum.Active:
                    return SubscriptionStatus.SubscriptionActive;
                case StatusEnum.EnumOnHold:
                    return SubscriptionStatus.SubscriptionPaused;
                case StatusEnum.Ended:
                    return SubscriptionStatus.SubscriptionStopped;
            }

            return SubscriptionStatus.SubscriptionUnknown;
        }
    }
}
