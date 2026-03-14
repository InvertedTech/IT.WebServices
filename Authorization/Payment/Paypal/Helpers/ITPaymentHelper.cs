using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Paypal.Clients.Models;
using IT.WebServices.Fragments.Authorization.Payment;

namespace IT.WebServices.Authorization.Payment.Paypal.Helpers
{
    internal static class ITPaymentHelper
    {
        public static GenericPaymentRecord ToGenericPaymentRecord(this TransactionInfoModel pRec)
        {
            var createdOn = pRec.transaction_initiation_date_UTC;
            var paidThru = createdOn.AddMonths(1).AddDays(2);

            return new()
            {
                ProcessorPaymentID = pRec.transaction_id ?? "",
                Status = pRec.StatusEnum,
                AmountCents = pRec.transaction_amount?.AmountInCents ?? 0,
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = pRec.transaction_amount?.AmountInCents ?? 0,
                CreatedOnUTC = Timestamp.FromDateTimeOffset(createdOn),
                ModifiedOnUTC = Timestamp.FromDateTimeOffset(createdOn),
                PaidOnUTC = Timestamp.FromDateTimeOffset(createdOn),
                PaidThruUTC = Timestamp.FromDateTimeOffset(paidThru),
            };
        }

        public static ProcessorPaymentRecord ToProcessorPaymentRecord(this TransactionInfoModel pRec)
        {
            var createdOn = pRec.transaction_initiation_date_UTC;
            var paidThru = createdOn.AddMonths(1).AddDays(2);

            return new()
            {
                ProcessorSubscriptionID = pRec.paypal_reference_id ?? "",
                ProcessorPaymentID = pRec.transaction_id ?? "",
                Status = pRec.StatusEnum,
                AmountCents = pRec.transaction_amount?.AmountInCents ?? 0,
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = pRec.transaction_amount?.AmountInCents ?? 0,
                CreatedOnUTC = createdOn,
                ModifiedOnUTC = createdOn,
                PaidOnUTC = createdOn,
                PaidThruUTC = paidThru,
            };
        }
    }
}
