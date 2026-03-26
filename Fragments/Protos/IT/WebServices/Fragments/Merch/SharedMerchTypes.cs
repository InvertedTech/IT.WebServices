using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Merch
{
    public sealed partial class MerchBulkActionProgress : pb::IMessage<MerchBulkActionProgress>
    {
        public bool IsCanceled => CanceledOnUTC != null;
        public bool IsCompleted => CompletedOnUTC != null;

        public bool IsCompletedOrCanceled => IsCompleted || IsCanceled;
    }
}
