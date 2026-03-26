using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Content.CMS.Services.Data;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Merch.Generic.Data;
using IT.WebServices.Merch.Jobs;

namespace IT.WebServices.Merch.Shopify.Jobs
{
    public class PullImagesFromShopifyProcessor : IPullImagesFromAllProcessor
    {
        private readonly IGenericMerchRecordProvider recordProvider;
        //private readonly IAssetDataProvider assetDataProvider;
        private readonly IHttpClientFactory httpClientFactory;

        public PullImagesFromShopifyProcessor(IGenericMerchRecordProvider recordProvider, /*IAssetDataProvider assetDataProvider,*/ IHttpClientFactory httpClientFactory)
        {
            this.recordProvider = recordProvider;
            //this.assetDataProvider = assetDataProvider;
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run(MerchBulkActionProgress progress, CancellationToken cancellationToken)
        {
            var client = httpClientFactory.CreateClient();
            var records = recordProvider.GetAll();
            int totalSaved = 0;
            int totalSkipped = 0;

            await foreach (var record in records)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var imagesToPull = GetImagesToPull(record).ToList();
                if (imagesToPull.Count == 0)
                    continue;

                for (int i = 0; i < imagesToPull.Count; i++)
                {
                    var image = imagesToPull[i];
                    progress.StatusMessage = $"Pulling image {i + 1}/{imagesToPull.Count} for {record.Title} ({totalSaved} saved, {totalSkipped} skipped)";

                    var response = await client.GetAsync(image.Url, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        totalSkipped++;
                        continue;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var filename = Path.GetFileName(new Uri(image.Url).LocalPath);

                    //var assetRecord = new AssetRecord
                    //{
                    //    Image = new ImageAssetRecord
                    //    {
                    //        Public = new ImageAssetPublicRecord
                    //        {
                    //            AssetID = Guid.NewGuid().ToString(),
                    //            CreatedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                    //            Data = new ImageAssetPublicData
                    //            {
                    //                Title = filename,
                    //                MimeType = contentType,
                    //                Data = ByteString.CopyFrom(bytes),
                    //            }
                    //        },
                    //        Private = new ImageAssetPrivateRecord()
                    //    }
                    //};

                    //await assetDataProvider.Save(assetRecord);
                    //image.ImageAssetID = assetRecord.Image.Public.AssetID;
                    totalSaved++;
                }

                await recordProvider.Save(record);
            }

            progress.CompletedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);
            progress.Progress = 100;
            progress.StatusMessage = $"Completed — {totalSaved} images saved, {totalSkipped} skipped";
        }

        private IEnumerable<GenericMerchImageRecord> GetImagesToPull(GenericMerchRecord record)
        {
            if (record.FeaturedImage is not null && !string.IsNullOrEmpty(record.FeaturedImage.Url) && string.IsNullOrEmpty(record.FeaturedImage.ImageAssetID))
                yield return record.FeaturedImage;

            foreach (var img in record.OtherImages)
                if (!string.IsNullOrEmpty(img.Url) && string.IsNullOrEmpty(img.ImageAssetID))
                    yield return img;

            foreach (var variant in record.Variants)
                if (variant.Image is not null && !string.IsNullOrEmpty(variant.Image.Url) && string.IsNullOrEmpty(variant.Image.ImageAssetID))
                    yield return variant.Image;
        }
    }
}
