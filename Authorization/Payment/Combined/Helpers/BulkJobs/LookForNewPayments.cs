using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using System.Diagnostics;
using FortisAPI.Standard.Models;
using IT.WebServices.Fragments.Generic;

namespace IT.WebServices.Authorization.Payment.Combined.Helpers.BulkJobs
{
    public abstract class LookForNewPayments : IBulkJob
    {
        private readonly ILogger logger;
        private readonly IGenericSubscriptionFullRecordProvider fullProvider;
        private readonly IGenericSubscriptionRecordProvider subProvider;
        private readonly IGenericPaymentRecordProvider paymentProvider;
        private readonly GenericPaymentProcessorProvider genericProcessorProvider;
        private readonly ReconcileHelper reconcileHelper;

        private Task? task;
        private CancellationTokenSource cancelToken = new();
        private ONUser user;

        public LookForNewPayments(ILogger logger, IGenericSubscriptionFullRecordProvider fullProvider, IGenericSubscriptionRecordProvider subProvider, IGenericPaymentRecordProvider paymentProvider, GenericPaymentProcessorProvider genericProcessorProvider, ReconcileHelper reconcileHelper)
        {
            this.logger = logger;
            this.fullProvider = fullProvider;
            this.subProvider = subProvider;
            this.paymentProvider = paymentProvider;
            this.genericProcessorProvider = genericProcessorProvider;
            this.reconcileHelper = reconcileHelper;
        }

        public abstract uint DaysToLookBack { get; }
        public abstract PaymentBulkActionProgress Progress { get; init; }

        public void Cancel(ONUser user)
        {
            cancelToken.Cancel();

            Progress.CanceledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
            Progress.CanceledBy = user.Id.ToString();
            Progress.Progress = 100;
            Progress.StatusMessage = "Canceled";
        }

        public void Start(ONUser user)
        {
            Progress.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
            Progress.CreatedBy = user.Id.ToString();
            Progress.Progress = 0;
            Progress.StatusMessage = "Starting";

            this.user = user;

            task = LoadAll();
        }

        private async Task LoadAll()
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var range = new DateTimeOffsetRange(now.AddDays(-DaysToLookBack), now);

                var processors = genericProcessorProvider.AllEnabledProviders;

                for (int i = 0; i < processors.Length; i++)
                {
                    Progress.Progress = 1F * i / processors.Length;
                    var processor = processors[i];

                    Progress.StatusMessage = $"Loading {processor.ProcessorName}";
                    var payments = processor.GetAllPaymentsForDateRange(range);

                    var j = 0;
                    await foreach (var payment in payments)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();

                        j++;
                        Progress.StatusMessage = $"Loading {processor.ProcessorName} - {j}";
                        await LoadPayment(payment);
                    }
                }

                Progress.StatusMessage = "Completed Successfully";
                Progress.CompletedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                Progress.Progress = 1;
            }
            catch (Exception ex)
            {
                Progress.StatusMessage = ex.Message;
                Progress.CompletedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
            }
        }

        private async Task LoadPayment(ProcessorPaymentRecord payment)
        {
            var localPay = await paymentProvider.GetByProcessorId(payment.ProcessorPaymentID);

            GenericSubscriptionRecord? localSub = null;

            if (localPay is not null)
                localSub = await subProvider.GetById(localPay.UserID.ToGuid(), localPay.InternalSubscriptionID.ToGuid());

            if (localSub is null)
                localSub = await subProvider.GetByProcessorId(payment.ProcessorSubscriptionID);

            if (localSub is null)
            {
                logger.LogWarning("Couldn't find sub {subId} for payment {payId}", payment.ProcessorSubscriptionID, payment.ProcessorPaymentID);
                return;
            }

            await reconcileHelper.EnsurePayment(localSub, payment.ToGenericPaymentRecord(), user);
        }
    }
}
