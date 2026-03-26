using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Merch;

namespace IT.WebServices.Merch.Combined.Helpers.BulkJobs
{
    public interface IBulkJob
    {
        public MerchBulkActionProgress Progress { get; }

        public void Cancel(ONUser user);
        public void Start(ONUser user);
    }
}
