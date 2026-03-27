using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Clients.CMS;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Fragments.Merch.Shopify;
using IT.WebServices.Helpers;
using IT.WebServices.Merch.Generic.Data;
using IT.WebServices.Merch.Jobs;
using Microsoft.Extensions.Logging;
using ShopifySharp;
using ShopifySharp.Credentials;
using ShopifySharp.Factories;
using System.Net.Http.Json;
using System.Text.Json;

namespace IT.WebServices.Merch.Shopify.Jobs
{
    public class PullFromShopifyProcessor : IPullFromAllProcessor
    {
        private readonly IGenericMerchRecordProvider recordProvider;
        private readonly SettingsHelper settingsClient;
        private readonly ILogger log;
        private readonly IProductServiceFactory productsService;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly AssetClient assetClient;

        private const string StorefrontQuery = """
            query CollectionProducts($id: ID!, $cursor: String) {
              collection(id: $id) {
                products(first: 250, after: $cursor) {
                  pageInfo {
                    hasNextPage
                    endCursor
                  }
                  edges {
                    node {
                      id
                      title
                      vendor
                      productType
                      tags
                      createdAt
                      publishedAt
                      description
                      availableForSale
                      featuredImage {
                        url
                        altText
                      }
                      images(first: 20) {
                        edges {
                          node {
                            url
                            altText
                          }
                        }
                      }
                      variants(first: 100) {
                        edges {
                          node {
                            id
                            sku
                            availableForSale
                            price {
                              amount
                            }
                            compareAtPrice {
                              amount
                            }
                            image {
                              url
                              altText
                            }
                            selectedOptions {
                              name
                              value
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        public PullFromShopifyProcessor(IGenericMerchRecordProvider recordProvider, SettingsHelper settingsClient, ILogger<PullFromShopifyProcessor> log, IProductServiceFactory productsService, IHttpClientFactory httpClientFactory, AssetClient assetClient)
        {
            this.recordProvider = recordProvider;
            this.settingsClient = settingsClient;
            this.log = log;
            this.productsService = productsService;
            this.httpClientFactory = httpClientFactory;
            this.assetClient = assetClient;
        }

        public async Task Run(MerchBulkActionProgress progress, CancellationToken cancellationToken)
        {
            if (!(settingsClient.Public.Merch?.Shopify?.IsEnabled ?? false))
                return;

            var stores = settingsClient.Owner.Merch?.Shopify?.Stores?.ToArray() ?? Array.Empty<ShopifyStoreConfig>();
            var numRuns = stores.Length;

            if (stores.Length == 0)
                return;

            var imageClient = httpClientFactory.CreateClient();

            for (int i = 0; i < stores.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var store = stores[i];
                ShopifyApiCredentials creds = new ShopifyApiCredentials(store.StorefrontDomain, store.StoreAdminToken);
                var service = productsService.Create(creds);

                progress.Progress = 1.0F * i / numRuns;
                progress.StatusMessage = $"Pulling from Shopify: {store.StoreName}";

                try
                {
                    var page = await service.ListAsync(new ShopifySharp.Filters.ProductListFilter { Limit = 250 });
                    await ProductItemsToRecords(page.Items, store.InternalStoreID, progress, i, numRuns, imageClient, cancellationToken);

                    while (page.HasNextPage)
                    {
                        page = await service.ListAsync(page.GetNextPageFilter(250));
                        await ProductItemsToRecords(page.Items, store.InternalStoreID, progress, i, numRuns, imageClient, cancellationToken);
                    }
                }
                catch (ShopifySharp.ShopifyHttpException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (store.CollectionIds.Count == 0)
                        continue;

                    progress.StatusMessage = $"Falling back to storefront pull: {store.StoreName}";
                    await FetchFromStorefrontAsync(store, progress, i, numRuns, imageClient, cancellationToken);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error pulling from Shopify store {StoreName}", store.StoreName);
                }

                await Task.Delay(10000, cancellationToken);
            }

            progress.CompletedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);
            progress.Progress = 100;
            progress.StatusMessage = "Completed";
        }

        private async Task FetchFromStorefrontAsync(ShopifyStoreConfig store, MerchBulkActionProgress progress, int storeIndex, int numStores, HttpClient imageClient, CancellationToken cancellationToken)
        {
            var client = CreateStorefrontClient(store);
            var apiUrl = $"https://{store.StorefrontDomain}/api/2024-10/graphql.json";
            var collectionIds = store.CollectionIds.ToList();

            for (int c = 0; c < collectionIds.Count; c++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var gid = $"gid://shopify/Collection/{collectionIds[c]}";
                string? cursor = null;

                do
                {
                    var (items, hasNextPage, endCursor) = await FetchStorefrontPageAsync(client, apiUrl, gid, cursor, store.StoreName, cancellationToken);
                    if (items is null)
                        break;

                    cursor = hasNextPage ? endCursor : null;

                    for (int j = 0; j < items.Count; j++)
                    {
                        var node = items[j];
                        progress.Progress = 1.0F * (storeIndex + (1.0F * j / items.Count)) / numStores;
                        progress.StatusMessage = $"Storefront pull: {node.GetProperty("title").GetString()} ({j + 1}/{items.Count})";

                        try
                        {
                            var rec = StorefrontNodeToRecord(node, store.InternalStoreID);
                            await recordProvider.Save(rec);
                            await PullImagesForRecord(rec, imageClient, cancellationToken);
                            await recordProvider.Save(rec);
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex.Message);
                        }
                    }
                } while (cursor != null);
            }
        }

        private HttpClient CreateStorefrontClient(ShopifyStoreConfig store)
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Shopify-Storefront-Access-Token", store.StoreAdminToken);
            return client;
        }

        private async Task<(List<JsonElement>? Items, bool HasNextPage, string? EndCursor)> FetchStorefrontPageAsync(
            HttpClient client, string apiUrl, string gid, string? cursor, string storeName, CancellationToken cancellationToken)
        {
            var body = new { query = StorefrontQuery, variables = new { id = gid, cursor } };
            var response = await client.PostAsJsonAsync(apiUrl, body, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                log.LogError("Storefront API returned {StatusCode} for store {StoreName}", response.StatusCode, storeName);
                return (null, false, null);
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var productsEl = doc.RootElement
                .GetProperty("data")
                .GetProperty("collection")
                .GetProperty("products");

            var pageInfo = productsEl.GetProperty("pageInfo");
            var hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
            var endCursor = hasNextPage ? pageInfo.GetProperty("endCursor").GetString() : null;

            var items = productsEl.GetProperty("edges")
                .EnumerateArray()
                .Select(e => e.GetProperty("node").Clone())
                .ToList();

            return (items, hasNextPage, endCursor);
        }

        private GenericMerchRecord StorefrontNodeToRecord(JsonElement node, string storeId)
        {
            var rec = new GenericMerchRecord()
            {
                InternalProductId = Guid.NewGuid().ToString(),
                InternalStoreId = storeId,
                Provider = MerchRecordProvider.ShopifyRecordProvider,
                ProcessorProductId = node.GetProperty("id").GetString() ?? "",
                Title = node.GetProperty("title").GetString() ?? "",
                Description = node.GetProperty("description").GetString() ?? "",
                Vendor = node.GetProperty("vendor").GetString() ?? "",
                ProductType = node.GetProperty("productType").GetString() ?? "",
                InStock = node.GetProperty("availableForSale").GetBoolean(),
            };

            if (node.TryGetProperty("tags", out var tagsEl))
                foreach (var tag in tagsEl.EnumerateArray())
                    rec.Tags.Add(tag.GetString() ?? "");

            if (node.TryGetProperty("createdAt", out var createdAt) && DateTimeOffset.TryParse(createdAt.GetString(), out var created))
                rec.CreatedOnUTC = Timestamp.FromDateTimeOffset(created);

            if (node.TryGetProperty("publishedAt", out var publishedAt) && DateTimeOffset.TryParse(publishedAt.GetString(), out var published))
                rec.PublishOnUTC = Timestamp.FromDateTimeOffset(published);

            if (node.TryGetProperty("featuredImage", out var featuredImage) && featuredImage.ValueKind != JsonValueKind.Null)
                rec.FeaturedImage = ParseStorefrontImage(featuredImage);

            if (node.TryGetProperty("images", out var imagesEl))
                foreach (var imgEdge in imagesEl.GetProperty("edges").EnumerateArray())
                    rec.OtherImages.Add(ParseStorefrontImage(imgEdge.GetProperty("node")));

            if (node.TryGetProperty("variants", out var variantsEl))
                foreach (var variantEdge in variantsEl.GetProperty("edges").EnumerateArray())
                    rec.Variants.Add(ParseStorefrontVariant(variantEdge.GetProperty("node")));

            return rec;
        }

        private static GenericMerchImageRecord ParseStorefrontImage(JsonElement el) =>
            new()
            {
                Url = el.GetProperty("url").GetString() ?? "",
                AltText = el.TryGetProperty("altText", out var alt) && alt.ValueKind != JsonValueKind.Null ? alt.GetString() ?? "" : ""
            };

        private static GenericMerchVariantRecord ParseStorefrontVariant(JsonElement v)
        {
            var variant = new GenericMerchVariantRecord()
            {
                ProcessorVariantId = v.GetProperty("id").GetString() ?? "",
                SKU = v.TryGetProperty("sku", out var sku) ? sku.GetString() ?? "" : "",
                InStock = v.TryGetProperty("availableForSale", out var vInStock) && vInStock.GetBoolean(),
                PriceCents = v.TryGetProperty("price", out var price) && decimal.TryParse(price.GetProperty("amount").GetString(), out var priceVal) ? (uint)(priceVal * 100) : 0,
                CompareAtPriceCents = v.TryGetProperty("compareAtPrice", out var cap) && cap.ValueKind != JsonValueKind.Null && decimal.TryParse(cap.GetProperty("amount").GetString(), out var capVal) ? (uint)(capVal * 100) : 0,
            };

            if (v.TryGetProperty("image", out var vImg) && vImg.ValueKind != JsonValueKind.Null)
                variant.Image = ParseStorefrontImage(vImg);

            if (v.TryGetProperty("selectedOptions", out var selectedOptions))
                foreach (var opt in selectedOptions.EnumerateArray())
                    variant.Options.Add(new GenericMerchVariantOption
                    {
                        Name = opt.GetProperty("name").GetString() ?? "",
                        Value = opt.GetProperty("value").GetString() ?? ""
                    });

            return variant;
        }

        private async Task ProductItemsToRecords(IEnumerable<ShopifySharp.Product> items, string storeId, MerchBulkActionProgress progress, int storeIndex, int numStores, HttpClient imageClient, CancellationToken cancellationToken)
        {
            var itemList = items.ToList();
            var itemCount = itemList.Count;

            for (int i = 0; i < itemCount; i++)
            {
                var item = itemList[i];
                progress.Progress = 1.0F * (storeIndex + (1.0F * i / itemCount)) / numStores;
                progress.StatusMessage = $"Pulling from Shopify: {item.Title} ({i + 1}/{itemCount})";
                try
                {
                    var rec = item.ProductToRecord(storeId);
                    await recordProvider.Save(rec);
                    await PullImagesForRecord(rec, imageClient, cancellationToken);
                    await recordProvider.Save(rec);
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                }
            }
        }

        private async Task PullImagesForRecord(GenericMerchRecord rec, HttpClient client, CancellationToken cancellationToken)
        {
            foreach (var image in GetImagesToPull(rec))
            {
                try
                {
                    var response = await client.GetAsync(image.Url, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var filename = Path.GetFileName(new Uri(image.Url).LocalPath);

                    var saved = await assetClient.SaveAsset(new CreateAssetRequest
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
                    });

                    if (saved is not null)
                        image.ImageAssetID = saved.AssetIDGuid.ToString();

                    await Task.Delay(500, cancellationToken);
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
