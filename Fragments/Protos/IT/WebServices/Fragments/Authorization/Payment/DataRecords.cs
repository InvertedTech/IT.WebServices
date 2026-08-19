using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Authorization.Payment
{
    public sealed partial class GenericSubscriptionFullRecord : pb::IMessage<GenericSubscriptionFullRecord>
    {
        public string ProcessorName => SubscriptionRecord.ProcessorName;

        public void CalculateRecords()
        {
            var last = Payments.Where(p => p.Status == PaymentStatus.PaymentComplete).OrderBy(p => p.PaidOnUTC).LastOrDefault();
            if (last == null)
                return;

            LastPaidUTC = last.PaidOnUTC;
            PaidThruUTC = last.PaidThruUTC;
            RenewsOnUTC = pb.WellKnownTypes.Timestamp.FromDateTimeOffset(last.PaidOnUTC.ToDateTimeOffset().AddMonths(1));
        }

        public GenericPaymentRecord? MostRecentPayment => Payments.OrderByDescending(p => p.PaidOnUTC).FirstOrDefault();
    }
}
