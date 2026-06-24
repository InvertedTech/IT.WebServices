using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Fortis.Helpers;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Helpers;

namespace IT.WebServices.Authorization.Payment.Fortis
{
    public class FortisGenericPaymentProcessor : IGenericPaymentProcessor
    {
        private readonly FortisContactHelper fortisContactHelper;
        private readonly FortisSubscriptionHelper fortisSubscriptionHelper;
        private readonly FortisTransactionHelper fortisTransactionHelper;
        private readonly IGenericSubscriptionRecordProvider genericSubProvider;
        private readonly SettingsHelper settingsHelper;
        private readonly IUserService userService;

        public FortisGenericPaymentProcessor(FortisContactHelper fortisContactHelper, FortisSubscriptionHelper fortisSubscriptionHelper, FortisTransactionHelper fortisTransactionHelper, IGenericSubscriptionRecordProvider genericSubProvider, SettingsHelper settingsHelper, IUserService userService)
        {
            this.fortisContactHelper = fortisContactHelper;
            this.fortisSubscriptionHelper = fortisSubscriptionHelper;
            this.fortisTransactionHelper = fortisTransactionHelper;
            this.genericSubProvider = genericSubProvider;
            this.settingsHelper = settingsHelper;
            this.userService = userService;
        }

        public string ProcessorName => PaymentConstants.PROCESSOR_NAME_FORTIS;

        public bool GetAllSubscriptionsSupported => true;

        public bool GetAllPaymentsBetweenDatesSupported => true;

        public bool GetMissingUserIdForSubscriptionSupported => true;

        public bool IsEnabled => settingsHelper.Public.Subscription.Fortis.Enabled;

        public async Task<CancelSubscriptionResponse> CancelSubscription(GenericSubscriptionRecord record, ONUser userToken, CancellationToken cancellationToken)
        {
            var res = await fortisSubscriptionHelper.Get(record.ProcessorSubscriptionID, cancellationToken);
            if (res == null)
            {
                res = await fortisSubscriptionHelper.Get(record.ProcessorSubscriptionID, cancellationToken, false);
                if (res == null)
                    return new() { Error = "SubscriptionId not valid" };
            }

            if (res.Status == SubscriptionStatus.SubscriptionActive)
            {
                await fortisSubscriptionHelper.Cancel(record.ProcessorSubscriptionID, cancellationToken);
                var cancelRes = await fortisSubscriptionHelper.Get(record.ProcessorSubscriptionID, cancellationToken, false);
                if (cancelRes?.Status != SubscriptionStatus.SubscriptionStopped)
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

        public IAsyncEnumerable<ProcessorPaymentRecord> GetAllPaymentsForDateRange(DateTimeOffsetRange range, CancellationToken cancellationToken) => fortisTransactionHelper.GetAllForRange(range, cancellationToken);

        public async Task<List<GenericPaymentRecord>> GetAllPaymentsForSubscription(string processorSubscriptionID, CancellationToken cancellationToken)
        {
            var res = await fortisSubscriptionHelper.GetWithTransactions(processorSubscriptionID, cancellationToken);
            return res?.Payments.ToList() ?? new();
        }

        public Task<List<GenericSubscriptionRecord>> GetAllSubscriptions(CancellationToken cancellationToken) => fortisSubscriptionHelper.GetAll(cancellationToken);

        public async Task<Guid> GetMissingUserIdForSubscription(GenericSubscriptionRecord subToFind, CancellationToken cancellationToken)
        {
            var fortisSub = await fortisSubscriptionHelper.Get(subToFind.ProcessorSubscriptionID, cancellationToken);
            if (fortisSub == null)
                return Guid.Empty;

            var contact = await fortisContactHelper.Get(fortisSub.ProcessorCustomerID);
            if (contact?.Data == null)
                return Guid.Empty;

            var apiId = contact.Data.ContactApiId;
            if (string.IsNullOrEmpty(apiId))
                return Guid.Empty;

            var user = await GetUser(apiId);
            if (user?.Record == null)
                return Guid.Empty;

            return user.Record.UserIDGuid;
        }

        private async Task<GetOtherPublicUserResponse?> GetUser(string id)
        {
            if (Guid.TryParse(id, out var guid))
            {
                var user = await userService.GetOtherPublicUserInternal(guid);
                if (user != null)
                    return user;
            }

            if (id.StartsWith("u"))
            {
                var withoutU = id.Substring(1);

                if (Guid.TryParse(withoutU, out var guid2))
                {
                    var user2 = await userService.GetOtherPublicUserInternal(guid2);
                    if (user2 != null)
                        return user2;
                }

                var user = await userService.GetUserByOldUserID(withoutU);
                if (user != null)
                    return user;
            }

            return await userService.GetUserByOldUserID(id);
        }

        public Task<GenericSubscriptionRecord?> GetSubscription(string processorSubscriptionID, CancellationToken cancellationToken) => fortisSubscriptionHelper.Get(processorSubscriptionID, cancellationToken);

        public Task<GenericSubscriptionFullRecord?> GetSubscriptionFull(string processorSubscriptionID, CancellationToken cancellationToken) => fortisSubscriptionHelper.GetWithTransactions(processorSubscriptionID, cancellationToken);
    }
}
