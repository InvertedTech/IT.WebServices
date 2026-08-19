using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using IT.WebServices.Authorization.Payment.Stripe.Clients;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Payment.Stripe
{
    public class StripeGenericPaymentProcessor : IGenericPaymentProcessor
    {
        private readonly StripeClient stripeClient;
        private readonly IGenericSubscriptionRecordProvider genericSubProvider;
        private readonly SettingsHelper settingsHelper;
        private readonly IUserService userService;

        public StripeGenericPaymentProcessor(StripeClient stripeClient, IGenericSubscriptionRecordProvider genericSubProvider, SettingsHelper settingsHelper, IUserService userService)
        {
            this.stripeClient = stripeClient;
            this.genericSubProvider = genericSubProvider;
            this.settingsHelper = settingsHelper;
            this.userService = userService;
        }

        public string ProcessorName => PaymentConstants.PROCESSOR_NAME_STRIPE;

        public bool GetAllSubscriptionsSupported => true;

        public bool GetAllPaymentsBetweenDatesSupported => true;

        public bool GetMissingUserIdForSubscriptionSupported => true;

        public bool IsEnabled => settingsHelper.Public.Subscription.Stripe.Enabled;

        public bool RerunFailedPaymentSupported => false;

        public async Task<CancelSubscriptionResponse> CancelSubscription(GenericSubscriptionRecord record, ONUser userToken, CancellationToken cancellationToken)
        {
            var res = await genericSubProvider.GetById(record.UserID.ToGuid(), record.InternalSubscriptionID.ToGuid());
            if (res == null)
                return new() { Error = "SubscriptionId not valid" };

            if (res.Status == SubscriptionStatus.SubscriptionActive)
            {
                var cancelRes = await stripeClient.CancelSubscription(record.ProcessorSubscriptionID, "", cancellationToken);
                if (!cancelRes)
                    return new() { Error = "Unable to cancel subscription" };
            }

            record.Status = SubscriptionStatus.SubscriptionStopped;
            record.CanceledBy = userToken.Id.ToString();
            record.CanceledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

            cancellationToken.ThrowIfCancellationRequested();

            await genericSubProvider.Save(record);

            return new()
            {
                Record = record
            };
        }

        public IAsyncEnumerable<ProcessorPaymentRecord> GetAllPaymentsForDateRange(DateTimeOffsetRange range, CancellationToken cancellationToken) => stripeClient.GetAllPaymentsForDateRange(range, cancellationToken);

        public Task<List<GenericPaymentRecord>> GetAllPaymentsForSubscription(string processorSubscriptionID, CancellationToken cancellationToken) => stripeClient.GetAllPaymentsForSubscription(processorSubscriptionID, cancellationToken);

        public Task<List<GenericSubscriptionRecord>> GetAllSubscriptions(CancellationToken cancellationToken) => stripeClient.GetAllSubscriptions(cancellationToken);

        public Task<Guid> GetMissingUserIdForSubscription(GenericSubscriptionRecord processorSubscription, CancellationToken cancellationToken) => stripeClient.GetMissingUserIdForSubscription(processorSubscription, cancellationToken);

        public Task<GenericSubscriptionRecord?> GetSubscription(string processorSubscriptionID, CancellationToken cancellationToken) => stripeClient.GetSubscription(processorSubscriptionID, cancellationToken);

        public Task<GenericSubscriptionFullRecord?> GetSubscriptionFull(string processorSubscriptionID, CancellationToken cancellationToken) => stripeClient.GetSubscriptionFull(processorSubscriptionID, cancellationToken);

        public Task<bool> ReRunFailedPayment(GenericPaymentRecord record, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
