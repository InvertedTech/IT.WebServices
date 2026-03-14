using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Fragments.Authorization.Payment;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Payment.Combined.Helpers.BulkJobs
{
    public class LookForNewPaymentsOneDay : LookForNewPayments
    {
        public LookForNewPaymentsOneDay(ILogger<LookForNewPaymentsOneDay> logger, IGenericSubscriptionFullRecordProvider fullProvider, IGenericSubscriptionRecordProvider subProvider, IGenericPaymentRecordProvider paymentProvider, GenericPaymentProcessorProvider genericProcessorProvider, ReconcileHelper reconcileHelper)
                : base(logger, fullProvider, subProvider, paymentProvider, genericProcessorProvider, reconcileHelper) { }

        public override uint DaysToLookBack => 1;

        public override PaymentBulkActionProgress Progress { get; init; } = new () { Action = PaymentBulkAction.LookForNewPaymentsOneDay };
    }
}
