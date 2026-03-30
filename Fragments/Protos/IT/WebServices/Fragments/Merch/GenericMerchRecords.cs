using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Merch
{
    public sealed partial class GenericMerchRecord : pb::IMessage<GenericMerchRecord>
    {
        public GenericMerchImageRecord? GetImageByUrl(string url)
        {
            if (FeaturedImage.Url == url)
                return FeaturedImage;

            foreach (var image in OtherImages)
                if (image.Url == url)
                    return image;

            foreach (var variant in Variants)
                if (variant.Image?.Url == url)
                    return variant.Image;

            return null;
        }
    }
}
