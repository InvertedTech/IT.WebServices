using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Combined.Helpers.BulkJobs;
using IT.WebServices.Fragments.Authorization.Payment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IT.WebServices.Authorization.Payment.Combined.Helpers
{
    public class BulkHelper
    {
        private readonly ILogger log;
        private readonly IServiceProvider serviceProvider;

        private readonly ConcurrentDictionary<PaymentBulkAction, IBulkJob> runningJobs = new();

        public BulkHelper(ILogger<BulkHelper> log, IServiceProvider serviceProvider)
        {
            this.log = log;
            this.serviceProvider = serviceProvider;
        }

        public List<PaymentBulkActionProgress> CancelAction(PaymentBulkAction action, ONUser user)
        {
            try
            {
                if (runningJobs.Remove(action, out var job))
                {
                    job.Cancel(user);
                }
            }
            catch { }

            return GetRunningActions();
        }

        public List<PaymentBulkActionProgress> GetRunningActions()
        {
            CheckAll();

            return runningJobs.Values.Select(j => j.Progress).ToList();
        }

        public List<PaymentBulkActionProgress> StartAction(PaymentBulkAction action, ONUser user)
        {
            var newJob = GetNewJob(action);
            if (newJob == null)
                return GetRunningActions();

            if (runningJobs.TryAdd(action, newJob))
            {
                newJob.Start(user);
            }

            return GetRunningActions();
        }

        private void CheckAll()
        {
            foreach (var kv in runningJobs)
            {
                if (kv.Value.Progress.IsCompletedOrCanceled)
                    runningJobs.TryRemove(kv);
            }
        }

        private IBulkJob? GetNewJob(PaymentBulkAction action)
        {
            switch (action)
            {
                case PaymentBulkAction.LookForNewPaymentsOneDay:
                    return serviceProvider.GetService<LookForNewPaymentsOneDay>();
                case PaymentBulkAction.LookForNewPaymentsOneMonth:
                    return serviceProvider.GetService<LookForNewPaymentsOneMonth>();
                case PaymentBulkAction.LookForNewPaymentsOneWeek:
                    return serviceProvider.GetService<LookForNewPaymentsOneWeek>();
                case PaymentBulkAction.ReconcileAll:
                    return serviceProvider.GetService<ReconcileAll>();
                case PaymentBulkAction.ReconcileStripeTaxRates:
                    return serviceProvider.GetService<ReconcileStripeTaxRates>();
            }

            return null;
        }
    }
}
