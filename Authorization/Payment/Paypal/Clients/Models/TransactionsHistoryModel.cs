using IT.WebServices.Fragments.Authorization.Payment;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Payment.Paypal.Clients.Models
{
    public class TransactionsHistoryModel : BasePaginated
    {
        public List<TransactionHistoryModel> transaction_details { get; set; } = new();
    }

    public class TransactionHistoryModel
    {
        public TransactionInfoModel? transaction_info { get; set; }
    }

    public class TransactionInfoModel
    {
        public string? transaction_id { get; set; }
        
        public string? transaction_status { get; set; }
        public PaymentStatus StatusEnum
        {
            get
            {
                switch (transaction_status)
                {
                    case "D":
                        return PaymentStatus.PaymentFailed;
                    case "P":
                        return PaymentStatus.PaymentPending;
                    case "S":
                        return PaymentStatus.PaymentComplete;
                    case "V":
                        return PaymentStatus.PaymentRefunded;
                }
                return PaymentStatus.PaymentUnknown;
            }
        }

        public string? paypal_reference_id { get; set; }
        public string? paypal_reference_id_type { get; set; }
        public AmountModel? transaction_amount { get; set; }
        public string? transaction_initiation_date { get; set; }
        public DateTimeOffset transaction_initiation_date_UTC
        {
            get
            {
                if (DateTimeOffset.TryParse(transaction_initiation_date, out var date))
                    return date;
                return DateTimeOffset.MinValue;
            }
        }

        public string? transaction_updated_date { get; set; }
        public DateTimeOffset? transaction_updated_date_UTC
        {
            get
            {
                if (DateTimeOffset.TryParse(transaction_updated_date, out var date))
                    return date;
                return null;
            }
        }

        public class AmountModel
        {
            public string? value { get; set; }

            public uint? AmountInCents
            {
                get
                {
                    if (!double.TryParse(value, out var amount))
                        return null;
                    return (uint)(amount * 100);
                }
            }

            public override string ToString()
            {
                return "$" + (value ?? "0.00");
            }
        }

        public override string ToString()
        {
            return "$" + (transaction_amount?.value ?? "0.00") + " " + transaction_updated_date;
        }
    }
}
