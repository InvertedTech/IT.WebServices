using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Merch.Jobs;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Merch.Combined.Helpers.BulkJobs
{
    public class PullFromAll : IBulkJob
    {
        private readonly IPullFromAllProcessor[] processors;
        private readonly ILogger log;

        private Task? task;
        private CancellationTokenSource cancelToken = new();

        public PullFromAll(IEnumerable<IPullFromAllProcessor> processors, ILogger<PullFromAll> log)
        {
            this.processors = processors.ToArray();
            this.log = log;
        }

        public CancellationToken CancelToken => cancelToken.Token;
        public MerchBulkActionProgress Progress { get; init; } = new() { Action = MerchBulkAction.PullFromAll };
        public ONUser StartedBy { get; private set; }

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
            StartedBy = user;

            Progress.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
            Progress.CreatedBy = user.Id.ToString();
            Progress.Progress = 0;
            Progress.StatusMessage = "Starting";

            task = Run();
        }

        private async Task Run()
        {
            foreach (var processor in this.processors)
            {
                if (cancelToken.Token.IsCancellationRequested)
                    return;

                try
                {
                    await processor.Run(Progress, cancelToken.Token);
                }
                catch (Exception e)
                {
                    log.LogError(e, "Error running processors");
                }
            }
        }
    }
}
