using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Stripe.Clients;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Payment.Combined.Helpers.BulkJobs
{
    public class ReconcileStripeTaxRates : IBulkJob
    {
        private readonly StripeClient stripeClient;

        private Task? task;
        private CancellationTokenSource cancelToken = new();

        public ReconcileStripeTaxRates(StripeClient stripeClient)
        {
            this.stripeClient = stripeClient;
        }

        public PaymentBulkActionProgress Progress { get; init; } = new() { Action = PaymentBulkAction.ReconcileStripeTaxRates };

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

            task = stripeClient.ReconcileStripeTaxRates(user, Progress, cancelToken.Token);
        }
    }
}
