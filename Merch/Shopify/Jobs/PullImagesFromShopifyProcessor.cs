using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Content.CMS;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Merch.Generic.Data;
using IT.WebServices.Merch.Jobs;

namespace IT.WebServices.Merch.Shopify.Jobs
{
    public class PullImagesFromShopifyProcessor : IPullImagesFromAllProcessor
    {
        private readonly IGenericMerchRecordProvider recordProvider;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IAssetService assetClient;
        public PullImagesFromShopifyProcessor(IGenericMerchRecordProvider recordProvider, IHttpClientFactory httpClientFactory, IAssetService assetClient)
        {
            this.recordProvider = recordProvider;
            this.httpClientFactory = httpClientFactory;
            this.assetClient = assetClient;
        }

        private IBulkJob job;

        public async Task Run(IBulkJob job)
        {
            this.job = job;

            var client = httpClientFactory.CreateClient();
            var records = recordProvider.GetAll();
            int totalSaved = 0;
            int totalSkipped = 0;

            await foreach (var record in records)
            {
                if (job.CancelToken.IsCancellationRequested)
                    return;

                var imagesToPull = GetImagesToPull(record).ToList();
                if (imagesToPull.Count == 0)
                    continue;

                for (int i = 0; i < imagesToPull.Count; i++)
                {
                    var image = imagesToPull[i];
                    job.Progress.StatusMessage = $"Pulling image {i + 1}/{imagesToPull.Count} for {record.Title} ({totalSaved} saved, {totalSkipped} skipped)";

                    var response = await client.GetAsync(image.Url, job.CancelToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        totalSkipped++;
                        continue;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(job.CancelToken);
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var filename = Path.GetFileName(new Uri(image.Url).LocalPath);

                    var asset = new ImageAssetData
                    {
                        Public = new ImageAssetPublicData
                        {
                            Title = filename,
                            MimeType = contentType,
                            Data = ByteString.CopyFrom(bytes),
                        },
                        Private = new()
                    };
                    var res = await assetClient.CreateAssetInternal(new CreateAssetRequest
                    {
                        Image = asset
                    }, job.StartedBy);

                    if (res is null)
                    {
                        totalSkipped++;
                        continue;
                    }

                    image.ImageAssetID = res.Record.AssetIDGuid.ToString();
                    totalSaved++;

                    await Task.Delay(500, job.CancelToken);
                }

                await recordProvider.Save(record);
            }

            job.Progress.CompletedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);
            job.Progress.Progress = 100;
            job.Progress.StatusMessage = $"Completed — {totalSaved} images saved, {totalSkipped} skipped";
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
