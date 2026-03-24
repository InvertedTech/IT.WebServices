using Grpc.Core;
using IT.WebServices.Fragments.Authorization.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Payments
{
    public class PaymentClient
    {
        private readonly ClientGrpcHelper grpcHelper;

        public PaymentClient(ClientGrpcHelper grpcHelper)
        {
            this.grpcHelper = grpcHelper;
        }

        public async Task<GenericSubscriptionFullRecord?> GetActiveSubscription(string platformUserId)
        {
            var client = new AdminPaymentInterface.AdminPaymentInterfaceClient(grpcHelper.PaymentServiceChannel);
            var res = await client.GetOtherSubscriptionRecordsAsync(
                new GetOtherSubscriptionRecordsRequest { UserID = platformUserId },
                GetMetadata()
            );

            return res?.Generic
                .Where(s => s.SubscriptionRecord.Status == SubscriptionStatus.SubscriptionActive)
                .OrderByDescending(s => s.PaidThruUTC)
                .FirstOrDefault();
        }

        private Metadata GetMetadata()
        {
            var data = new Metadata();
            data.Add("Authorization", "Bearer " + grpcHelper.ServiceToken.Value);
            return data;
        }
    }

}
