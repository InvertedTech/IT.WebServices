using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Fragments.Authorization.Payment;
using Stripe;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Payment.Stripe.Helpers
{
    internal static class ITPaymentHelper
    {
        public static GenericPaymentRecord ToGenericPaymentRecord(this Invoice pRec)
        {
            var createdOn = pRec.Created;
            var paidThru = createdOn.AddMonths(1).AddDays(2);

            var subTotal = (uint)pRec.Subtotal;
            var total = (uint)pRec.Total;
            var tax = total - subTotal;
            var taxRate = 1.0 * tax / subTotal;
            var taxRatePercent = taxRate * 100;

            return new()
            {
                ProcessorPaymentID = pRec.Id,
                Status = ConvertStatus(pRec.Status),
                AmountCents = subTotal,
                TaxCents = tax,
                TaxRateThousandPercents = (uint)(taxRatePercent * 1000),
                TotalCents = total,
                CreatedOnUTC = Timestamp.FromDateTime(pRec.Created),
                ModifiedOnUTC = Timestamp.FromDateTime(pRec.Created),
                PaidOnUTC = Timestamp.FromDateTime(pRec.Created),
                PaidThruUTC = Timestamp.FromDateTime(paidThru),
            };
        }

        public static ProcessorPaymentRecord ToProcessorPaymentRecord(this Invoice pRec)
        {
            var createdOn = pRec.Created;
            var paidThru = createdOn.AddMonths(1).AddDays(2);

            var subTotal = (uint)pRec.Subtotal;
            var total = (uint)pRec.Total;
            var tax = total - subTotal;
            var taxRate = 1.0 * tax / subTotal;
            var taxRatePercent = taxRate * 100;

            return new()
            {
                ProcessorSubscriptionID = pRec.Parent.SubscriptionDetails.SubscriptionId,
                ProcessorPaymentID = pRec.Id,
                Status = ConvertStatus(pRec.Status),
                AmountCents = subTotal,
                TaxCents = tax,
                TaxRateThousandPercents = (uint)(taxRatePercent * 1000),
                TotalCents = total,
                CreatedOnUTC = new DateTimeOffset(pRec.Created, TimeSpan.Zero),
                ModifiedOnUTC = new DateTimeOffset(pRec.Created, TimeSpan.Zero),
                PaidOnUTC = new DateTimeOffset(pRec.Created, TimeSpan.Zero),
                PaidThruUTC = new DateTimeOffset(paidThru, TimeSpan.Zero),
            };
        }

        private static PaymentStatus ConvertStatus(string status)
        {
            switch (status)
            {
                case "draft":
                case "open":
                case "requires_payment_method":
                case "requires_confirmation":
                case "requires_capture":
                case "requires_action":
                case "processing":
                    return PaymentStatus.PaymentPending;
                case "paid":
                case "succeeded":
                    return PaymentStatus.PaymentComplete;
                case "canceled":
                case "uncollectible":
                case "void":
                default:
                    return PaymentStatus.PaymentFailed;
            }
        }
    }
}
