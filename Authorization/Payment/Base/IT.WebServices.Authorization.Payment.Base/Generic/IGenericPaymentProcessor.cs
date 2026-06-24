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

        Task<CancelSubscriptionResponse> CancelSubscription(GenericSubscriptionRecord record, ONUser userToken, CancellationToken cancellationToken);

        Task<List<GenericSubscriptionRecord>> GetAllSubscriptions(CancellationToken cancellationToken);
        bool GetAllSubscriptionsSupported { get; }

        IAsyncEnumerable<ProcessorPaymentRecord> GetAllPaymentsForDateRange(DateTimeOffsetRange range, CancellationToken cancellationToken);
        bool GetAllPaymentsBetweenDatesSupported { get; }

        Task<List<GenericPaymentRecord>> GetAllPaymentsForSubscription(string processorSubscriptionID, CancellationToken cancellationToken);

        Task<Guid> GetMissingUserIdForSubscription(GenericSubscriptionRecord processorSubscription, CancellationToken cancellationToken);
        bool GetMissingUserIdForSubscriptionSupported { get; }

        Task<GenericSubscriptionRecord?> GetSubscription(string processorSubscriptionID, CancellationToken cancellationToken);
        Task<GenericSubscriptionFullRecord?> GetSubscriptionFull(string processorSubscriptionID, CancellationToken cancellationToken);
    }
}
