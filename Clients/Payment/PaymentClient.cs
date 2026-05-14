using Microsoft.Extensions.Logging;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Authorization.Payment;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using IT.WebServices.Fragments.Authorization.Payment.Stripe;

namespace IT.WebServices.Clients.Payment
{
    public class PaymentClient
    {
        private readonly PaymentInterface.PaymentInterfaceClient client;
        private readonly StripeInterface.StripeInterfaceClient stripe;
        private readonly ONUserHelper userHelper;
        private readonly ILogger log;

        public PaymentClient(PaymentInterface.PaymentInterfaceClient client, StripeInterface.StripeInterfaceClient stripe, ONUserHelper userHelper, ILogger<PaymentClient> log)
        {
            this.client = client;
            this.stripe = stripe;
            this.userHelper = userHelper;
            this.log = log;
        }

        public async Task<GetSubscriptionRecordsResponse> GetOwnSubscriptions(GetOwnSubscriptionRecordsRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                return await client.GetOwnSubscriptionRecordsAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetOwnSubscriptions");
                return new();
            }
        }

        public async Task<GetNewDetailsResponse> NewSubscription(GetNewDetailsRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetNewDetailsAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in NewSubscription");
                return new();
            }
        }

        public async Task<CancelSubscriptionResponse> CancelSubscription(CancelOwnSubscriptionRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                return await client.CancelOwnSubscriptionAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in CancelSubscription");
                return new();
            }
        }

        public async Task<StripeFinishOwnSubscriptionResponse> FinishStripe(StripeFinishOwnSubscriptionRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                return await stripe.StripeFinishOwnSubscriptionAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in Finish Stripe");
                return new();
            }
        }
    }
}
