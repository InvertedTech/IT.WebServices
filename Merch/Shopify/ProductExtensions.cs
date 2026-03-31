using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.Merch;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShopifySharp
{
    public static class ProductExtensions
    {
        public static GenericMerchRecord ProductToRecord(this Product item, string storeId)
        {
            var options = item.Options?.ToList() ?? new();
            var variants = item.Variants?.ToList() ?? new();
            var images = item.Images?.ToList() ?? new();

            var rec = new GenericMerchRecord()
            {
                InternalProductId = Guid.NewGuid().ToString(),
                InternalStoreId = storeId,
                Provider = MerchRecordProvider.ShopifyRecordProvider,
                ProcessorProductId = item.Id.ToString(),
                Title = item.Title,
                Description = item.BodyHtml,
                Vendor = item.Vendor,
                ProductType = item.ProductType,
                Url = item.Handle,
                InStock = variants.Any(v => (v.InventoryQuantity ?? 0) > 0),
                CreatedOnUTC = Timestamp.FromDateTimeOffset(item.CreatedAt ?? new DateTimeOffset()),
                ModifiedOnUTC = Timestamp.FromDateTimeOffset(item.UpdatedAt ?? new DateTimeOffset()),
            };

            if (item.PublishedAt is not null)
            {
                rec.PublishOnUTC = Timestamp.FromDateTimeOffset(item.PublishedAt.Value);
            }

            if (!string.IsNullOrEmpty(item.Tags))
                rec.Tags.Add(item.Tags);

            MapProductImages(rec, images);
            MapProductVariants(rec, variants, options, images);

            rec.PriceCents = rec.Variants.Where(v => v.PriceCents > 0).OrderBy(v => v.PriceCents).Select(v => v.PriceCents).FirstOrDefault();

            return rec;
        }

        public static void MapProductImages(GenericMerchRecord rec, List<ProductImage> images)
        {
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
        }

        public static void MapProductVariants(GenericMerchRecord rec, List<ProductVariant> variants, List<ProductOption> options, List<ProductImage> images)
        {
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
        }
    }
}
