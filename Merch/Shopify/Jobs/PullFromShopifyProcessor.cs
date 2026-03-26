using Google.Protobuf.WellKnownTypes;
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

        public PullFromShopifyProcessor(IGenericMerchRecordProvider recordProvider, SettingsHelper settingsClient, ILogger<PullFromShopifyProcessor> log, IProductServiceFactory productsService, IHttpClientFactory httpClientFactory)
        {
            this.recordProvider = recordProvider;
            this.settingsClient = settingsClient;
            this.log = log;
            this.productsService = productsService;
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run(MerchBulkActionProgress progress, CancellationToken cancellationToken)
        {
            if (!(settingsClient.Public.Merch?.Shopify?.IsEnabled ?? false))
                return;

            var stores = settingsClient.Owner.Merch?.Shopify?.Stores?.ToArray() ?? Array.Empty<ShopifyStoreConfig>();
            var numRuns = stores.Length;

            if (stores.Length == 0)
                return;

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
                    await ProductItemsToRecords(page.Items, store.InternalStoreID, progress, i, numRuns);

                    while (page.HasNextPage)
                    {
                        page = await service.ListAsync(page.GetNextPageFilter(250));
                        await ProductItemsToRecords(page.Items, store.InternalStoreID, progress, i, numRuns);
                    }
                }
                catch (ShopifySharp.ShopifyHttpException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (store.CollectionIds.Count == 0)
                        continue;

                    progress.StatusMessage = $"Falling back to storefront pull: {store.StoreName}";
                    await FetchFromStorefrontAsync(store, progress, i, numRuns, cancellationToken);
                }

                await Task.Delay(10000, cancellationToken);
            }

            progress.CompletedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);
            progress.Progress = 100;
            progress.StatusMessage = "Completed";
        }

        private async Task FetchFromStorefrontAsync(ShopifyStoreConfig store, MerchBulkActionProgress progress, int storeIndex, int numStores, CancellationToken cancellationToken)
        {
            const string query = """
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

            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Shopify-Storefront-Access-Token", store.StoreAdminToken);

            var collectionIds = store.CollectionIds.ToList();
            for (int c = 0; c < collectionIds.Count; c++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var gid = $"gid://shopify/Collection/{collectionIds[c]}";
                var apiUrl = $"https://{store.StorefrontDomain}/api/2024-10/graphql.json";
                string? cursor = null;

                do
                {
                    var body = new { query, variables = new { id = gid, cursor } };
                    var response = await client.PostAsJsonAsync(apiUrl, body, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        log.LogError("Storefront API returned {StatusCode} for store {StoreName}", response.StatusCode, store.StoreName);
                        break;
                    }

                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    var productsEl = doc.RootElement
                        .GetProperty("data")
                        .GetProperty("collection")
                        .GetProperty("products");

                    var pageInfo = productsEl.GetProperty("pageInfo");
                    var hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
                    cursor = hasNextPage ? pageInfo.GetProperty("endCursor").GetString() : null;

                    var edges = productsEl.GetProperty("edges");
                    var items = edges.EnumerateArray().Select(e => e.GetProperty("node")).ToList();

                    for (int j = 0; j < items.Count; j++)
                    {
                        var node = items[j];
                        progress.Progress = 1.0F * (storeIndex + (1.0F * j / items.Count)) / numStores;
                        progress.StatusMessage = $"Storefront pull: {node.GetProperty("title").GetString()} ({j + 1}/{items.Count})";

                        try
                        {
                            var rec = new GenericMerchRecord()
                            {
                                InternalProductId = Guid.NewGuid().ToString(),
                                InternalStoreId = store.InternalStoreID,
                                Provider = MerchRecordProvider.Shopify,
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
                                rec.FeaturedImage = new GenericMerchImageRecord
                                {
                                    Url = featuredImage.GetProperty("url").GetString() ?? "",
                                    AltText = featuredImage.TryGetProperty("altText", out var featAlt) && featAlt.ValueKind != JsonValueKind.Null ? featAlt.GetString() ?? "" : ""
                                };

                            if (node.TryGetProperty("images", out var imagesEl))
                                foreach (var imgEdge in imagesEl.GetProperty("edges").EnumerateArray())
                                {
                                    var img = imgEdge.GetProperty("node");
                                    rec.OtherImages.Add(new GenericMerchImageRecord
                                    {
                                        Url = img.GetProperty("url").GetString() ?? "",
                                        AltText = img.TryGetProperty("altText", out var imgAlt) && imgAlt.ValueKind != JsonValueKind.Null ? imgAlt.GetString() ?? "" : ""
                                    });
                                }

                            if (node.TryGetProperty("variants", out var variantsEl))
                                foreach (var variantEdge in variantsEl.GetProperty("edges").EnumerateArray())
                                {
                                    var v = variantEdge.GetProperty("node");
                                    var variant = new GenericMerchVariantRecord()
                                    {
                                        ProcessorVariantId = v.GetProperty("id").GetString() ?? "",
                                        SKU = v.TryGetProperty("sku", out var sku) ? sku.GetString() ?? "" : "",
                                        InStock = v.TryGetProperty("availableForSale", out var vInStock) && vInStock.GetBoolean(),
                                        PriceCents = v.TryGetProperty("price", out var price) && decimal.TryParse(price.GetProperty("amount").GetString(), out var priceVal) ? (uint)(priceVal * 100) : 0,
                                        CompareAtPriceCents = v.TryGetProperty("compareAtPrice", out var cap) && cap.ValueKind != JsonValueKind.Null && decimal.TryParse(cap.GetProperty("amount").GetString(), out var capVal) ? (uint)(capVal * 100) : 0,
                                    };

                                    if (v.TryGetProperty("image", out var vImg) && vImg.ValueKind != JsonValueKind.Null)
                                        variant.Image = new GenericMerchImageRecord
                                        {
                                            Url = vImg.GetProperty("url").GetString() ?? "",
                                            AltText = vImg.TryGetProperty("altText", out var vImgAlt) && vImgAlt.ValueKind != JsonValueKind.Null ? vImgAlt.GetString() ?? "" : ""
                                        };

                                    if (v.TryGetProperty("selectedOptions", out var selectedOptions))
                                        foreach (var opt in selectedOptions.EnumerateArray())
                                            variant.Options.Add(new GenericMerchVariantOption
                                            {
                                                Name = opt.GetProperty("name").GetString() ?? "",
                                                Value = opt.GetProperty("value").GetString() ?? ""
                                            });

                                    rec.Variants.Add(variant);
                                }

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

        private async Task ProductItemsToRecords(IEnumerable<ShopifySharp.Product> items, string StoreId, MerchBulkActionProgress progress, int storeIndex, int numStores)
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
                    var options = item.Options?.ToList() ?? new();
                    var variants = item.Variants?.ToList() ?? new();

                    var rec = new GenericMerchRecord()
                    {
                        InternalProductId = Guid.NewGuid().ToString(),
                        InternalStoreId = StoreId,
                        Provider = MerchRecordProvider.Shopify,
                        ProcessorProductId = item.Id.ToString(),
                        Title = item.Title,
                        Description = item.BodyHtml,
                        Vendor = item.Vendor,
                        ProductType = item.ProductType,
                        Url = item.Handle,
                        InStock = variants.Any(v => (v.InventoryQuantity ?? 0) > 0),
                        CreatedOnUTC = Timestamp.FromDateTimeOffset(item.CreatedAt ?? new DateTimeOffset()),
                        ModifiedOnUTC = Timestamp.FromDateTimeOffset(item.UpdatedAt ?? new DateTimeOffset()),
                        PublishOnUTC = Timestamp.FromDateTimeOffset(item.PublishedAt ?? new DateTimeOffset())
                    };

                    if (!string.IsNullOrEmpty(item.Tags))
                        rec.Tags.Add(item.Tags);

                    var images = item.Images?.ToList() ?? new();

                    var featuredImg = images.FirstOrDefault();
                    if (featuredImg is not null)
                        rec.FeaturedImage = new GenericMerchImageRecord
                        {
                            Url = featuredImg.Src ?? "",
                            AltText = featuredImg.Alt ?? ""
                        };

                    foreach (var img in images.Skip(1))
                        rec.OtherImages.Add(new GenericMerchImageRecord
                        {
                            Url = img.Src ?? "",
                            AltText = img.Alt ?? ""
                        });

                    foreach (var v in variants)
                    {
                        var variant = new GenericMerchVariantRecord()
                        {
                            ProcessorVariantId = v.Id.ToString(),
                            SKU = v.SKU ?? "",
                            PriceCents = (uint)((v.Price ?? 0) * 100),
                            CompareAtPriceCents = (uint)((v.CompareAtPrice ?? 0) * 100),
                            InStock = (v.InventoryQuantity ?? 0) > 0,
                        };

                        if (v.ImageId is not null)
                        {
                            var vImg = images.FirstOrDefault(img => img.Id == v.ImageId);
                            if (vImg is not null)
                                variant.Image = new GenericMerchImageRecord
                                {
                                    Url = vImg.Src ?? "",
                                    AltText = vImg.Alt ?? ""
                                };
                        }

                        var optionValues = new[] { v.Option1, v.Option2, v.Option3 };
                        for (int o = 0; o < optionValues.Length; o++)
                        {
                            if (string.IsNullOrEmpty(optionValues[o])) continue;
                            variant.Options.Add(new GenericMerchVariantOption
                            {
                                Name = options.ElementAtOrDefault(o)?.Name ?? $"Option{o + 1}",
                                Value = optionValues[o]
                            });
                        }

                        rec.Variants.Add(variant);
                    }

                    await recordProvider.Save(rec);
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                }
            }
        }
    }
}
