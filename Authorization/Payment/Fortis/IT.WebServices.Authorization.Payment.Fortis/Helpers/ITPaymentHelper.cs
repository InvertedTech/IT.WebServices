using FortisAPI.Standard.Models;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Fortis;
using Microsoft.AspNetCore.Http.HttpResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Payment.Fortis.Helpers
{
    internal static class ITPaymentHelper
    {
        public static GenericPaymentRecord ToGenericPaymentRecord(this Data14 fRec)
        {
            var createdOn = DateTimeOffset.FromUnixTimeSeconds(fRec.CreatedTs);
            var paidThru = createdOn.AddMonths(1).AddDays(2);
            return new()
            {
                ProcessorPaymentID = fRec.Id,
                Status = ConvertStatus(fRec.StatusCode),
                AmountCents = (uint)(fRec.TransactionAmount),
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = (uint)(fRec.TransactionAmount),
                CreatedOnUTC = Timestamp.FromDateTimeOffset(createdOn.UtcDateTime),
                ModifiedOnUTC = Timestamp.FromDateTimeOffset(createdOn.UtcDateTime),
                PaidOnUTC = Timestamp.FromDateTimeOffset(createdOn.UtcDateTime),
                PaidThruUTC = Timestamp.FromDateTimeOffset(paidThru.UtcDateTime),
            };
        }

        public static ProcessorPaymentRecord ToProcessorPaymentRecord(this Data14 fRec)
        {
            var createdOn = DateTimeOffset.FromUnixTimeSeconds(fRec.CreatedTs);
            var paidThru = createdOn.AddMonths(1).AddDays(2);
            return new()
            {
                ProcessorSubscriptionID = fRec.RecurringId,
                ProcessorPaymentID = fRec.Id,
                Status = ConvertStatus(fRec.StatusCode),
                AmountCents = (uint)(fRec.TransactionAmount),
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = (uint)(fRec.TransactionAmount),
                CreatedOnUTC = createdOn,
                ModifiedOnUTC = createdOn,
                PaidOnUTC = createdOn,
                PaidThruUTC = paidThru.UtcDateTime,
            };
        }

        public static GenericPaymentRecord ToGenericPaymentRecord(this List11 fRec)
        {
            var paidOn = DateTimeOffset.FromUnixTimeSeconds(fRec.CreatedTs);
            var paidThru = paidOn.AddMonths(1).AddDays(2);
            return new()
            {
                ProcessorPaymentID = fRec.Id,
                Status = ConvertStatus(fRec.StatusId),
                AmountCents = (uint)(fRec.TransactionAmountInt),
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = (uint)(fRec.TransactionAmountInt),
                CreatedOnUTC = Timestamp.FromDateTimeOffset(paidOn.UtcDateTime),
                ModifiedOnUTC = Timestamp.FromDateTimeOffset(paidOn.UtcDateTime),
                PaidOnUTC = Timestamp.FromDateTimeOffset(paidOn.UtcDateTime),
                PaidThruUTC = Timestamp.FromDateTimeOffset(paidThru.UtcDateTime),
            };
        }

        public static ProcessorPaymentRecord ToProcessorPaymentRecord(this List11 fRec)
        {
            var paidOn = DateTimeOffset.FromUnixTimeSeconds(fRec.CreatedTs);
            var paidThru = paidOn.AddMonths(1).AddDays(2);
            return new()
            {
                ProcessorSubscriptionID = fRec.RecurringId,
                ProcessorPaymentID = fRec.Id,
                Status = ConvertStatus(fRec.StatusId),
                AmountCents = (uint)(fRec.TransactionAmountInt),
                TaxCents = 0,
                TaxRateThousandPercents = 0,
                TotalCents = (uint)(fRec.TransactionAmountInt),
                CreatedOnUTC = paidOn,
                ModifiedOnUTC = paidOn,
                PaidOnUTC = paidOn,
                PaidThruUTC = paidThru,
            };
        }

        public static GenericPaymentRecord ToGenericPaymentRecord(this ResponseTransaction fRec) => fRec.Data.ToGenericPaymentRecord();
        public static ProcessorPaymentRecord ToProcessorPaymentRecord(this ResponseTransaction fRec) => fRec.Data.ToProcessorPaymentRecord();

        public static List<GenericPaymentRecord> ToGenericPaymentRecords(this ResponseTransactionsCollection fRec)
        {
            return fRec?.List
                        .Select(r => r?.ToGenericPaymentRecord())
                        .Where(r => r is not null)
                        .Select(r => r!)
                        .ToList() ?? new List<GenericPaymentRecord>();
        }

        public static List<ProcessorPaymentRecord> ToProcessorPaymentRecords(this ResponseTransactionsCollection fRec)
        {
            return fRec?.List
                        .Select(r => r?.ToProcessorPaymentRecord())
                        .Where(r => r is not null)
                        .Select(r => r!)
                        .ToList() ?? new List<ProcessorPaymentRecord>();
        }

        public static PaymentStatus ConvertStatus(StatusId2Enum? statusId)
        {
            switch (statusId)
            {
                case StatusId2Enum.Enum101:
                    return PaymentStatus.PaymentComplete;
            }

            return PaymentStatus.PaymentFailed;
        }
    }
}
