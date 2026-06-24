using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using IT.WebServices.Authorization.Payment.Paypal.Clients;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;

namespace IT.WebServices.Authorization.Payment.Paypal
{
    public class PaypalGenericPaymentProcessor : IGenericPaymentProcessor
    {
        private readonly PaypalClient paypalClient;
        private readonly IGenericSubscriptionRecordProvider genericSubProvider;
        private readonly SettingsHelper settingsHelper;
        private readonly IUserService userService;

        public PaypalGenericPaymentProcessor(PaypalClient paypalClient, IGenericSubscriptionRecordProvider genericSubProvider, SettingsHelper settingsHelper, IUserService userService)
        {
            this.paypalClient = paypalClient;
            this.genericSubProvider = genericSubProvider;
            this.settingsHelper = settingsHelper;
            this.userService = userService;
        }

        public string ProcessorName => PaymentConstants.PROCESSOR_NAME_PAYPAL;

        public bool GetAllSubscriptionsSupported => true;

        public bool GetAllPaymentsBetweenDatesSupported => true;

        public bool GetMissingUserIdForSubscriptionSupported => true;

        public bool IsEnabled => settingsHelper.Public.Subscription.Paypal.Enabled;

        public async Task<CancelSubscriptionResponse> CancelSubscription(GenericSubscriptionRecord record, ONUser userToken, CancellationToken cancellationToken)
        {
            var res = await genericSubProvider.GetById(record.UserID.ToGuid(), record.InternalSubscriptionID.ToGuid());
            if (res == null)
                return new() { Error = "SubscriptionId not valid" };

            if (res.Status == SubscriptionStatus.SubscriptionActive)
            {
                var cancelRes = await paypalClient.CancelSubscription(record.ProcessorSubscriptionID, "");
                if (!cancelRes)
                    return new() { Error = "Unable to cancel subscription" };
            }

            record.Status = SubscriptionStatus.SubscriptionStopped;
            record.CanceledBy = userToken.Id.ToString();
            record.CanceledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

            await genericSubProvider.Save(record);

            return new()
            {
                Record = record
            };
        }

        public IAsyncEnumerable<ProcessorPaymentRecord> GetAllPaymentsForDateRange(DateTimeOffsetRange range, CancellationToken cancellationToken) => paypalClient.GetTransactionsByDateSegmented(range, cancellationToken);

        public Task<List<GenericPaymentRecord>> GetAllPaymentsForSubscription(string processorSubscriptionID, CancellationToken cancellationToken) => Task.FromResult(new List<GenericPaymentRecord>()); // paypalClient.GetAllPaymentsForSubscription(processorSubscriptionID);

        public Task<List<GenericSubscriptionRecord>> GetAllSubscriptions(CancellationToken cancellationToken) => Task.FromResult(new List<GenericSubscriptionRecord>()); // paypalClient.GetAllSubscriptions();

        public Task<Guid> GetMissingUserIdForSubscription(GenericSubscriptionRecord processorSubscription, CancellationToken cancellationToken) => Task.FromResult(Guid.Empty); // paypalClient.GetMissingUserIdForSubscription(processorSubscription);

        public Task<GenericSubscriptionRecord?> GetSubscription(string processorSubscriptionID, CancellationToken cancellationToken) => Task.FromResult<GenericSubscriptionRecord?>(null); // paypalClient.GetSubscription(processorSubscriptionID);

        public Task<GenericSubscriptionFullRecord?> GetSubscriptionFull(string processorSubscriptionID, CancellationToken cancellationToken) => Task.FromResult<GenericSubscriptionFullRecord?>(null); // paypalClient.GetSubscriptionFull(processorSubscriptionID);
    }
}
