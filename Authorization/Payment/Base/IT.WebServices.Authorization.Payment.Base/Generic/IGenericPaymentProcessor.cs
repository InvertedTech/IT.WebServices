using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using IT.WebServices.Fragments.Authorization.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Payment.Generic
{
    public interface IGenericPaymentProcessor
    {
        string ProcessorName { get; }
        bool IsEnabled { get; }

        Task<CancelSubscriptionResponse> CancelSubscription(GenericSubscriptionRecord record, ONUser userToken);

        Task<List<GenericSubscriptionRecord>> GetAllSubscriptions();
        bool GetAllSubscriptionsSupported { get; }

        IAsyncEnumerable<ProcessorPaymentRecord> GetAllPaymentsForDateRange(DateTimeOffsetRange range);
        bool GetAllPaymentsBetweenDatesSupported { get; }

        Task<List<GenericPaymentRecord>> GetAllPaymentsForSubscription(string processorSubscriptionID);

        Task<Guid> GetMissingUserIdForSubscription(GenericSubscriptionRecord processorSubscription);
        bool GetMissingUserIdForSubscriptionSupported { get; }

        Task<GenericSubscriptionRecord?> GetSubscription(string processorSubscriptionID);
        Task<GenericSubscriptionFullRecord?> GetSubscriptionFull(string processorSubscriptionID);
    }
}
