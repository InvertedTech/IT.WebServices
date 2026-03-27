using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Helpers;
using IT.WebServices.Merch.Generic.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace IT.WebServices.Merch.Combined.Services
{
    public class MerchService : MerchInterface.MerchInterfaceBase
    {
        private readonly ILogger logger;
        private readonly IGenericMerchRecordProvider recordProvider;

        public MerchService(ILogger<MerchService> logger, IGenericMerchRecordProvider recordProvider)
        {
            this.logger = logger;
            this.recordProvider = recordProvider;
        }

        [AllowAnonymous]
        public override async Task<GetMerchResponse> GetMerch(GetMerchRequest request, ServerCallContext context)
        {
            try
            {
                return new()
                {
                    Record = await recordProvider.GetById(request.InternalProductId.ToGuid()),
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GetMerch");
            }

            return new();
        }

        [AllowAnonymous]
        public override async Task<SearchMerchResponse> SearchMerch(SearchMerchRequest request, ServerCallContext context)
        {
            try
            {
                if (request.PageSize == 0)
                    request.PageSize = 50;

                var searchQueryBits = Array.Empty<string>();
                var searchInternalStoreId = request.InternalStoreId;
                var searchProviders = request.Providers?.ToArray();
                var searchTag = request.Tag?.ToLower();
                var searchInStockOnly = request.OnlyInStock;

                if (!string.IsNullOrWhiteSpace(request.Query))
                    searchQueryBits = request.Query.ToLower().Replace("\"", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray();

                if (string.IsNullOrWhiteSpace(searchInternalStoreId))
                    searchInternalStoreId = null;
                if (searchProviders?.Length == 0)
                    searchProviders = null;
                if (string.IsNullOrWhiteSpace(searchTag))
                    searchTag = null;

                var res = new SearchMerchResponse();

                List<GenericMerchRecord> list = new();
                await foreach (var rec in recordProvider.GetAll())
                {
                    if (!CanShowInList(rec))
                        continue;

                    if (request.PriceRangeSearch != null)
                    {
                        if (rec.PriceCents < request.PriceRangeSearch.MinimumRange)
                            continue;
                        if (rec.PriceCents > request.PriceRangeSearch.MaximumRange)
                            continue;
                    }

                    if (searchInternalStoreId != null)
                        if (rec.InternalStoreId != searchInternalStoreId)
                            continue;

                    if (searchProviders != null)
                        if (!searchProviders.Any(p => p == rec.Provider))
                            continue;

                    if (searchTag != null)
                        if (!rec.Tags.Select(t => t.ToLower()).Contains(searchTag))
                            continue;

                    if (searchInStockOnly)
                        if (!rec.InStock)
                            continue;

                    if (searchQueryBits.Length > 0)
                    {
                        if (!MeetsQuery(searchQueryBits, request.Query, rec))
                            continue;
                    }

                    list.Add(rec);
                }

                res.Records.AddRange(list.OrderByDescending(r => r.PublishOnUTC));
                res.PageTotalItems = (uint)res.Records.Count;

                if (request.PageSize > 0)
                {
                    res.PageOffsetStart = request.PageOffset;

                    var page = res.Records.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                    res.Records.Clear();
                    res.Records.AddRange(page);
                }

                res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

                return res;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in SearchMerch");
            }

            return new();
        }

        private bool CanShowInList(GenericMerchRecord rec)
        {
            if (rec.PublishOnUTC == null || rec.PublishOnUTC > Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow))
                return false;

            return true;
        }

        private bool MeetsQuery(string[] searchQueryBits, string searchQuery, GenericMerchRecord rec)
        {
            if (MeetsQuery(searchQueryBits, rec.Title.ToLower()))
                return true;

            if (MeetsQuery(searchQueryBits, rec.Description.ToLower()))
                return true;

            if (MeetsQuery(searchQueryBits, rec.SKU.ToLower()))
                return true;

            foreach (var tag in rec.Tags)
                if (MeetsQuery(searchQueryBits, tag.ToLower()))
                    return true;

            return false;
        }

        private bool MeetsQuery(string[] searchQueryBits, string haystack)
        {
            foreach (string bit in searchQueryBits)
                if (haystack.Contains(bit))
                    return true;

            return false;
        }
    }
}
