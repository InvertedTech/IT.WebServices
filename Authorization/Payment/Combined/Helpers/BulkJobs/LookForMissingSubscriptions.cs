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
    public class LookForMissingSubscriptions : IBulkJob
    {
        private Task? task;
        private CancellationTokenSource cancelToken = new();
        private readonly ReconcileHelper reconcileHelper;

        public LookForMissingSubscriptions(ReconcileHelper reconcileHelper)
        {
            this.reconcileHelper = reconcileHelper;
        }

        public PaymentBulkActionProgress Progress { get; init; } = new() { Action = PaymentBulkAction.LookForMissingSubscriptions };

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

            task = reconcileHelper.ReconcileNewOnly(user, Progress, cancelToken.Token);
        }
    }
}
