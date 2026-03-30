using Google.Protobuf;
using IT.WebServices.Content.CMS;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Merch.Jobs;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Merch.Helpers
{
    public class ImagePullHelper
    {
        private readonly IAssetService assetClient;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger log;

        private readonly HttpClient httpClient;

        public ImagePullHelper(IAssetService assetClient, IHttpClientFactory httpClientFactory, ILogger<ImagePullHelper> log)
        {
            this.assetClient = assetClient;
            this.httpClientFactory = httpClientFactory;
            this.log = log;

            httpClient = httpClientFactory.CreateClient();
        }

        public async Task PullImagesForRecord(GenericMerchRecord rec, IBulkJob job)
        {
            foreach (var image in GetImagesToPull(rec))
            {
                try
                {
                    var response = await httpClient.GetAsync(image.Url, job.CancelToken);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    var bytes = await response.Content.ReadAsByteArrayAsync(job.CancelToken);
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var filename = Path.GetFileName(new Uri(image.Url).LocalPath);

                    var saved = await assetClient.CreateAssetInternal(new CreateAssetRequest
                    {
                        Image = new ImageAssetData
                        {
                            Public = new ImageAssetPublicData
                            {
                                Title = filename,
                                MimeType = contentType,
                                Data = ByteString.CopyFrom(bytes),
                            },
                            Private = new()
                        }
                    }, job.StartedBy);

                    if (saved is not null)
                        image.ImageAssetID = saved.Record.AssetIDGuid.ToString();
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                }
            }
        }

        private IEnumerable<GenericMerchImageRecord> GetImagesToPull(GenericMerchRecord record)
        {
            if (record.FeaturedImage is not null
                && !string.IsNullOrEmpty(record.FeaturedImage.Url)
                && string.IsNullOrEmpty(record.FeaturedImage.ImageAssetID))
                yield return record.FeaturedImage;

            foreach (var img in record.OtherImages)
                if (!string.IsNullOrEmpty(img.Url)
                    && string.IsNullOrEmpty(img.ImageAssetID))
                    yield return img;

            foreach (var variant in record.Variants)
                if (variant.Image is not null && !string.IsNullOrEmpty(variant.Image.Url) && string.IsNullOrEmpty(variant.Image.ImageAssetID))
                    yield return variant.Image;
        }
    }
}
