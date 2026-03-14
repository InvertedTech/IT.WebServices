using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Fortis.Helpers;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using IT.WebServices.Authorization.Payment.Paypal.Clients.Models;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Stripe;

namespace IT.WebServices.Authorization.Payment.Combined.Helpers
{
    public class ReconcileHelper
    {
        private readonly ILogger logger;
        private readonly IGenericSubscriptionFullRecordProvider fullProvider;
        private readonly IGenericSubscriptionRecordProvider subProvider;
        private readonly IGenericPaymentRecordProvider paymentProvider;
        private readonly GenericPaymentProcessorProvider genericProcessorProvider;

        private const int YEARS_TO_GO_BACK_FOR_RECONCILE_ALL = 10;

        private const float PROGRESS_PERCENT_TO_GRAB_ALL_SUBS = 0.01F;
        private const float PROGRESS_PERCENT_TO_LOOK_FOR_NEW_SUBS = 0.1F;
        private const float PROGRESS_PERCENT_EXISTING_START = PROGRESS_PERCENT_TO_LOOK_FOR_NEW_SUBS;
        private const float PROGRESS_PERCENT_EXISTING_INCREASE = 1 - PROGRESS_PERCENT_EXISTING_START;
        private const float PROGRESS_PERCENT_NEW_START = 0;
        private const float PROGRESS_PERCENT_NEW_INCREASE = PROGRESS_PERCENT_TO_LOOK_FOR_NEW_SUBS - PROGRESS_PERCENT_TO_GRAB_ALL_SUBS;


        public ReconcileHelper(ILogger<ReconcileHelper> logger, IGenericSubscriptionFullRecordProvider fullProvider, IGenericSubscriptionRecordProvider subProvider, IGenericPaymentRecordProvider paymentProvider, GenericPaymentProcessorProvider genericProcessorProvider)
        {
            this.logger = logger;
            this.fullProvider = fullProvider;
            this.subProvider = subProvider;
            this.paymentProvider = paymentProvider;
            this.genericProcessorProvider = genericProcessorProvider;
        }

        public async Task ReconcileAll(ONUser user, PaymentBulkActionProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                await ReconcileNew(user, progress, cancellationToken);
                await ReconcileExisting(user, progress, cancellationToken);

                progress.StatusMessage = "Completed Successfully";
                progress.CompletedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                progress.Progress = 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ReconcileAll");
                progress.StatusMessage = ex.Message;
                progress.CompletedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
            }
        }

        private async Task ReconcileExisting(ONUser user, PaymentBulkActionProgress progress, CancellationToken cancellationToken)
        {
            var localSubs = await subProvider.GetAll().ToList();

            var numSubs = localSubs.Count();

            progress.Progress = PROGRESS_PERCENT_EXISTING_START;

            var i = 0;
            foreach (var localSub in localSubs)
            {
                i++;
                cancellationToken.ThrowIfCancellationRequested();

                var fullLocalSub = await fullProvider.GetBySubscription(localSub);
                if (fullLocalSub != null)
                {
                    await ReconcileSubscription(fullLocalSub, user);
                }

                progress.Progress = PROGRESS_PERCENT_EXISTING_INCREASE * i / numSubs + PROGRESS_PERCENT_EXISTING_START;
            }
        }

        private async Task ReconcileNew(ONUser user, PaymentBulkActionProgress progress, CancellationToken cancellationToken)
        {
            progress.Progress = PROGRESS_PERCENT_NEW_START;

            var localSubs = await subProvider.GetAll().ToList();
            var processorSubs = await GetAllSubscriptionsFromAllProcessors();

            var existingProcessorSubIds = localSubs.Select(s => s.ProcessorSubscriptionID);
            var missingSubs = processorSubs.Where(s => !existingProcessorSubIds.Contains(s.ProcessorSubscriptionID)).ToList();

            var numSubs = missingSubs.Count();

            progress.Progress = PROGRESS_PERCENT_TO_GRAB_ALL_SUBS;

            var i = 0;
            foreach (var missingSub in missingSubs)
            {
                i++;
                cancellationToken.ThrowIfCancellationRequested();

                await CreateMissingSubscription(missingSub, user);

                progress.Progress = PROGRESS_PERCENT_NEW_INCREASE * i / numSubs + PROGRESS_PERCENT_TO_GRAB_ALL_SUBS;
            }
        }

        public async Task<ReconcileSubscriptionResponse> ReconcileSubscription(GenericSubscriptionFullRecord localSub, ONUser user)
        {
            try
            {
                var processor = genericProcessorProvider.GetProcessor(localSub);
                if (processor == null)
                    return new() { Error = $"Processor ({localSub.ProcessorName}) not found" };

                var processorSub = await processor.GetSubscriptionFull(localSub.SubscriptionRecord.ProcessorSubscriptionID);
                if (processorSub == null)
                    return new() { Error = "SubscriptionId not valid" };

                await EnsureSubscription(localSub.SubscriptionRecord, processorSub.SubscriptionRecord, user);
                foreach (var processorPayment in processorSub.Payments)
                    await EnsurePayment(localSub.SubscriptionRecord, processorPayment, user);

                var updatedSub = await fullProvider.GetBySubscriptionId(localSub.SubscriptionRecord.UserID.ToGuid(), localSub.SubscriptionRecord.InternalSubscriptionID.ToGuid());

                return new() { Record = updatedSub ?? new() };
            }
            catch
            {
                return new() { Error = "Unknown error" };
            }
        }

        private async Task EnsureSubscription(GenericSubscriptionRecord localSub, GenericSubscriptionRecord processorSub, ONUser user)
        {
            bool changed = false;

            if (processorSub.Status == SubscriptionStatus.SubscriptionUnknown)
                return;

            if (localSub.Status != processorSub.Status)
            {
                localSub.Status = processorSub.Status;
                changed = true;
            }

            if (localSub.TotalCents != processorSub.TotalCents)
            {
                localSub.TotalCents = processorSub.TotalCents;
                localSub.AmountCents = processorSub.TotalCents;
                localSub.TaxCents = 0;
                changed = true;
            }

            if (changed)
            {
                localSub.ModifiedBy = user.Id.ToString();
                localSub.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                logger.LogInformation("Updating sub {subId}", localSub.ProcessorSubscriptionID);
                await subProvider.Save(localSub);
            }
        }

        public async Task EnsurePayment(GenericSubscriptionRecord localSub, GenericPaymentRecord processorPayment, ONUser user)
        {
            if (processorPayment.Status == PaymentStatus.PaymentUnknown)
                return;

            var localPayments = paymentProvider.GetAllBySubscriptionId(localSub.UserID.ToGuid(), localSub.InternalSubscriptionID.ToGuid());
            var localPayment = localPayments.ToBlockingEnumerable().FirstOrDefault(p => p.ProcessorPaymentID.ToLower() == processorPayment.ProcessorPaymentID.ToLower());

            if (localPayment == null)
            {
                await CreateMissingPayment(localSub, processorPayment, user);
                return;
            }

            bool changed = false;

            if (localPayment.Status != processorPayment.Status)
            {
                localPayment.Status = processorPayment.Status;
                changed = true;
            }

            if (localPayment.PaidOnUTC != processorPayment.PaidOnUTC)
            {
                localPayment.PaidOnUTC = processorPayment.PaidOnUTC;
                changed = true;
            }

            if (localPayment.PaidThruUTC != processorPayment.PaidThruUTC)
            {
                localPayment.PaidThruUTC = processorPayment.PaidThruUTC;
                changed = true;
            }

            //if (localSub.TotalCents != localPayment.TotalCents)
            //{
            //    localPayment.AmountCents = localSub.AmountCents;
            //    localPayment.TaxCents = localSub.TaxCents;
            //    localPayment.TaxRateThousandPercents = localSub.TaxRateThousandPercents;
            //    localPayment.TotalCents = localSub.TotalCents;
            //    changed = true;
            //}

            if (changed)
            {
                localPayment.ModifiedBy = user.Id.ToString();
                localPayment.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                logger.LogInformation("Updating payment {payId}", localPayment.ProcessorPaymentID);
                await paymentProvider.Save(localPayment);
            }
        }

        private async Task CreateMissingPayment(GenericSubscriptionRecord localSub, GenericPaymentRecord processorPayment, ONUser user)
        {
            if (processorPayment.TotalCents == 0)
                return;

            processorPayment.UserID = localSub.UserID;
            processorPayment.InternalSubscriptionID = localSub.InternalSubscriptionID;
            processorPayment.InternalPaymentID = Guid.NewGuid().ToString();
            processorPayment.CreatedBy = user.Id.ToString();
            processorPayment.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

            if (localSub.TotalCents == processorPayment.TotalCents)
            {
                processorPayment.AmountCents = localSub.AmountCents;
                processorPayment.TaxCents = localSub.TaxCents;
                processorPayment.TaxRateThousandPercents = localSub.TaxRateThousandPercents;
                processorPayment.TotalCents = localSub.TotalCents;
            }

            logger.LogInformation("Creating payment {payId}", processorPayment.ProcessorPaymentID);
            await paymentProvider.Save(processorPayment);
        }

        private async Task CreateMissingSubscription(GenericSubscriptionRecord processorSubscription, ONUser user)
        {
            if (processorSubscription.TotalCents == 0)
                return;

            var subUserId = await GetMissingUserIdForSubscription(processorSubscription);
            if (subUserId == Guid.Empty)
                return;

            processorSubscription.UserID = subUserId.ToString();
            processorSubscription.InternalSubscriptionID = Guid.NewGuid().ToString();
            processorSubscription.CreatedBy = user.Id.ToString();
            processorSubscription.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

            logger.LogInformation("Creating sub {subId}", processorSubscription.ProcessorSubscriptionID);
            await subProvider.Save(processorSubscription);
        }

        private async Task<List<GenericSubscriptionRecord>> GetAllSubscriptionsFromAllProcessors()
        {
            var list = new List<GenericSubscriptionRecord>();

            foreach (var processor in genericProcessorProvider.AllEnabledProviders)
            {
                if (processor.GetAllSubscriptionsSupported)
                {
                    list.AddRange(await processor.GetAllSubscriptions());
                    continue;
                }

                if (processor.GetAllPaymentsBetweenDatesSupported)
                {
                    var now = DateTimeOffset.UtcNow;
                    var range = new DateTimeOffsetRange(now.AddYears(-1), now);
                    var payments = processor.GetAllPaymentsForDateRange(range);

                    var innerHashSetOfProcessorSubIds = new HashSet<string>();

                    await foreach (var payment in payments)
                    {
                        if (!innerHashSetOfProcessorSubIds.Contains(payment.ProcessorSubscriptionID))
                        {
                            innerHashSetOfProcessorSubIds.Add(payment.ProcessorSubscriptionID);

                            var processorSub = HallucinateSubscriptionFromPayment(payment);
                            if (processorSub is not null)
                            {
                                processorSub.ProcessorName = processor.ProcessorName;
                                list.Add(processorSub);
                            }
                        }
                    }
                }
            }

            return list;
        }

        private Task<Guid> GetMissingUserIdForSubscription(GenericSubscriptionRecord processorSubscription)
        {
            var provider = genericProcessorProvider.GetProcessor(processorSubscription);
            if (!provider.GetMissingUserIdForSubscriptionSupported)
                return Task.FromResult(Guid.Empty);

            return provider.GetMissingUserIdForSubscription(processorSubscription);
        }

        private GenericSubscriptionRecord? HallucinateSubscriptionFromPayment(ProcessorPaymentRecord payment)
        {
            return new()
            {
                ProcessorSubscriptionID = payment.ProcessorSubscriptionID,
                AmountCents = payment.AmountCents,
                TaxCents = payment.TaxCents,
                TaxRateThousandPercents = payment.TaxRateThousandPercents,
                TotalCents = payment.TotalCents,
            };
        }
    }
}
