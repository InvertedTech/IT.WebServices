using IT.WebServices.Fragments.Merch;

namespace IT.WebServices.Merch.Jobs
{
    public interface IPullFromAllProcessor
    {
        public Task Run(IBulkJob job);
    }
}
