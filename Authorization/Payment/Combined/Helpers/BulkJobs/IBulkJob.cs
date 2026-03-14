using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Authorization.Payment;

namespace IT.WebServices.Authorization.Payment.Combined.Helpers.BulkJobs
{
    public interface IBulkJob
    {
        public PaymentBulkActionProgress Progress { get; }

        public void Cancel(ONUser user);
        public void Start(ONUser user);
    }
}
