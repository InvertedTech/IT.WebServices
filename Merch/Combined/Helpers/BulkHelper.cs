using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Merch.Combined.Helpers.BulkJobs;
using IT.WebServices.Merch.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IT.WebServices.Merch.Combined.Helpers
{
    public class BulkHelper
    {
        private readonly ILogger log;
        private readonly IServiceProvider serviceProvider;

        private readonly ConcurrentDictionary<MerchBulkAction, IBulkJob> runningJobs = new();

        public BulkHelper(ILogger<BulkHelper> log, IServiceProvider serviceProvider)
        {
            this.log = log;
            this.serviceProvider = serviceProvider;
        }

        public List<MerchBulkActionProgress> CancelAction(MerchBulkAction action, ONUser user)
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

        public List<MerchBulkActionProgress> GetRunningActions()
        {
            CheckAll();

            return runningJobs.Values.Select(j => j.Progress).ToList();
        }

        public List<MerchBulkActionProgress> StartAction(MerchBulkAction action, ONUser user)
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

        private IBulkJob? GetNewJob(MerchBulkAction action)
        {
            switch (action)
            {
                case MerchBulkAction.PullFromAll:
                    return serviceProvider.GetService<PullFromAll>();
            }

            return null;
        }
    }
}
