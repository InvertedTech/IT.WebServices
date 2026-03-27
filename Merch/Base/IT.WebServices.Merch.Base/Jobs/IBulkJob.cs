using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Merch;

namespace IT.WebServices.Merch.Jobs
{
    public interface IBulkJob
    {
        public CancellationToken CancelToken { get; }
        public MerchBulkActionProgress Progress { get; }
        public ONUser StartedBy { get; }

        public void Cancel(ONUser user);
        public void Start(ONUser user);
    }
}
