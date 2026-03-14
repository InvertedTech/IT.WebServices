using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Payment.Combined.Helpers.BulkJobs
{
    public class ReconcileAll : IBulkJob
    {
        private Task? task;
        private CancellationTokenSource cancelToken = new();
        private readonly ReconcileHelper reconcileHelper;

        public ReconcileAll(ReconcileHelper reconcileHelper)
        {
            this.reconcileHelper = reconcileHelper;
        }

        public PaymentBulkActionProgress Progress { get; init; } = new() { Action = PaymentBulkAction.ReconcileAll };

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

            task = reconcileHelper.ReconcileAll(user, Progress, cancelToken.Token);
        }
    }
}
