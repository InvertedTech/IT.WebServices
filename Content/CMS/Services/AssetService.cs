using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Content.CMS.Services.Data;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Fragments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Content.CMS.Services
{
    [Authorize(Roles = ONUser.ROLE_CAN_CREATE_CONTENT)]
    public class AssetService : AssetInterface.AssetInterfaceBase
    {
        private readonly ILogger logger;
        private readonly IAssetDataProvider dataProvider;

        public AssetService(ILogger<AssetService> logger, IAssetDataProvider dataProvider)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
        }

        [Authorize(Roles = ONUser.ROLE_CAN_CREATE_CONTENT)]
        public override async Task<CreateAssetResponse> CreateAsset(
            CreateAssetRequest request,
            ServerCallContext context
        )
        {
            if (!IsValid(request))
                return CreateAssetError(
                    APIErrorReason.ErrorReasonValidationFailed,
                    "Invalid Request Body"
                );

            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
                return CreateAssetError(
                    APIErrorReason.ErrorReasonUnauthenticated,
                    "Not Authenticated"
                );

            AssetRecord record = new();

            switch (request.CreateAssetRequestOneofCase)
            {
                case CreateAssetRequest.CreateAssetRequestOneofOneofCase.Audio:
                    return await CreateAudio(request.Audio, user);
                case CreateAssetRequest.CreateAssetRequestOneofOneofCase.Image:
                    return await CreateImage(request.Image, user);
                default:
                    return CreateAssetError(
                        APIErrorReason.ErrorReasonInvalidContent,
                        "Unsupported Asset Type"
                    );
            }
        }

        private async Task<CreateAssetResponse> CreateAudio(AudioAssetData audio, ONUser user)
        {
            AssetRecord record = new()
            {
                Audio = new()
                {
                    Public = new()
                    {
                        AssetID = Guid.NewGuid().ToString(),
                        CreatedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                        Data = audio.Public,
                    },
                    Private = new() { CreatedBy = user.Id.ToString(), Data = audio.Private },
                },
            };

            await dataProvider.Save(record);

            return new() { Record = new() { Audio = record.Audio } };
        }

        private async Task<CreateAssetResponse> CreateImage(ImageAssetData image, ONUser user)
        {
            AssetRecord record = new()
            {
                Image = new()
                {
                    Public = new()
                    {
                        AssetID = Guid.NewGuid().ToString(),
                        CreatedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                        Data = image.Public,
                    },
                    Private = new() { CreatedBy = user.Id.ToString(), Data = image.Private },
                },
            };

            await dataProvider.Save(record);

            return new() { Record = new() { Image = record.Image } };
        }

        [AllowAnonymous]
        public override async Task<GetAssetResponse> GetAsset(
            GetAssetRequest request,
            ServerCallContext context
        )
        {
            Guid contentId = request.AssetID.ToGuid();
            if (contentId == Guid.Empty)
                return GetAssetError(APIErrorReason.ErrorReasonInvalidContent, "Invalid AssetID");

            var rec = await dataProvider.GetById(contentId);
            if (rec == null)
                return GetAssetError(APIErrorReason.ErrorReasonNotFound, "Asset Not Found");

            switch (rec.AssetRecordOneofCase)
            {
                case AssetRecord.AssetRecordOneofOneofCase.Audio:
                    return new() { Audio = rec.Audio.Public };
                case AssetRecord.AssetRecordOneofOneofCase.Image:
                    return new() { Image = rec.Image.Public };
                default:
                    return GetAssetError(
                        APIErrorReason.ErrorReasonInvalidContent,
                        "Unsupported Asset Type"
                    );
            }
        }

        [Authorize(Roles = ONUser.ROLE_CAN_CREATE_CONTENT)]
        public override async Task<GetAssetAdminResponse> GetAssetAdmin(
            GetAssetAdminRequest request,
            ServerCallContext context
        )
        {
            Guid contentId = request.AssetID.ToGuid();
            if (contentId == Guid.Empty)
                return GetAssetAdminError(
                    APIErrorReason.ErrorReasonInvalidContent,
                    "Invalid AssetID"
                );

            var rec = await dataProvider.GetById(contentId);
            if (rec == null)
                return GetAssetAdminError(APIErrorReason.ErrorReasonNotFound, "Asset Not Found");

            return new() { Record = rec };
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<GetAssetByOldContentIDResponse> GetAssetByOldContentID(
            GetAssetByOldContentIDRequest request,
            ServerCallContext context
        )
        {
            var oldId = request.OldAssetID;
            if (oldId == "")
                return GetAssetByOldContentIDError(
                    APIErrorReason.ErrorReasonInvalidContent,
                    "OldAssetID is required"
                );

            var rec = await dataProvider.GetByOldAssetId(oldId);
            if (rec == null)
                return GetAssetByOldContentIDError(
                    APIErrorReason.ErrorReasonNotFound,
                    "Asset Not Found"
                );

            return new() { Record = rec };
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task GetListOfIDs(
            GetListOfIDsRequest request,
            IServerStreamWriter<GetListOfIDsResponse> responseStream,
            ServerCallContext context
        )
        {
            try
            {
                await foreach (var r in dataProvider.GetAll())
                {
                    switch (r.AssetRecordOneofCase)
                    {
                        case AssetRecord.AssetRecordOneofOneofCase.Audio:
                            if (r.Audio.Private.Data.OldAssetID != "")
                                await responseStream.WriteAsync(
                                    new()
                                    {
                                        AssetID = r.Audio.Public.AssetID,
                                        ModifiedOnUTC = r.Audio.Public.ModifiedOnUTC,
                                    }
                                );
                            break;
                        case AssetRecord.AssetRecordOneofOneofCase.Image:
                            if (r.Image.Private.Data.OldAssetID != "")
                                await responseStream.WriteAsync(
                                    new()
                                    {
                                        AssetID = r.Image.Public.AssetID,
                                        ModifiedOnUTC = r.Image.Public.ModifiedOnUTC,
                                    }
                                );
                            break;
                    }
                }
            }
            catch { }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task GetListOfOldContentIDs(
            GetListOfOldContentIDsRequest request,
            IServerStreamWriter<GetListOfOldContentIDsResponse> responseStream,
            ServerCallContext context
        )
        {
            try
            {
                await foreach (var r in dataProvider.GetAll())
                {
                    switch (r.AssetRecordOneofCase)
                    {
                        case AssetRecord.AssetRecordOneofOneofCase.Audio:
                            if (r.Audio.Private.Data.OldAssetID != "")
                                await responseStream.WriteAsync(
                                    new()
                                    {
                                        AssetID = r.Audio.Public.AssetID,
                                        OldAssetID = r.Audio.Private.Data.OldAssetID,
                                        ModifiedOnUTC = r.Audio.Public.ModifiedOnUTC,
                                    }
                                );
                            break;
                        case AssetRecord.AssetRecordOneofOneofCase.Image:
                            if (r.Image.Private.Data.OldAssetID != "")
                                await responseStream.WriteAsync(
                                    new()
                                    {
                                        AssetID = r.Image.Public.AssetID,
                                        OldAssetID = r.Image.Private.Data.OldAssetID,
                                        ModifiedOnUTC = r.Image.Public.ModifiedOnUTC,
                                    }
                                );
                            break;
                    }
                }
            }
            catch { }
        }

        [Authorize(Roles = ONUser.ROLE_CAN_CREATE_CONTENT)]
        public override async Task<SearchAssetResponse> SearchAsset(
            SearchAssetRequest request,
            ServerCallContext context
        )
        {
            var searchQueryBits = Array.Empty<string>();

            if (!string.IsNullOrWhiteSpace(request.Query))
                searchQueryBits = request
                    .Query.ToLower()
                    .Replace("\"", " ")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToArray();

            var res = new SearchAssetResponse();

            List<AssetListRecord> list = new();
            await foreach (var rec in dataProvider.GetAllShort())
            {
                AssetListRecord listRec = null;
                switch (rec.AssetType)
                {
                    case AssetType.AssetAudio:
                        if (request.AssetType == AssetType.AssetImage)
                            continue;

                        listRec = rec;
                        break;
                    case AssetType.AssetImage:
                        if (request.AssetType == AssetType.AssetAudio)
                            continue;

                        listRec = rec;
                        break;
                }

                if (listRec == null)
                    continue;

                if (searchQueryBits.Length > 0)
                {
                    if (!MeetsQuery(searchQueryBits, listRec))
                        continue;
                }

                list.Add(listRec);
            }

            res.Records.AddRange(list.OrderByDescending(r => r.CreatedOnUTC));
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res
                    .Records.Skip((int)request.PageOffset)
                    .Take((int)request.PageSize)
                    .ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        [AllowAnonymous]
        public override async Task<SearchAssetResponse> GetImageAssets(
            GetImageAssetsRequest request,
            ServerCallContext context
        )
        {
            try
            {

                var searchQueryBits = Array.Empty<string>();

                if (!string.IsNullOrWhiteSpace(request.Query))
                    searchQueryBits = request
                        .Query.ToLower()
                        .Replace("\"", " ")
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .ToArray();

                var res = new SearchAssetResponse();
                var list = await this.dataProvider.GetByAssetTypeAsync(AssetType.AssetImage);

                if (list == null)
                    return res;

                var filtered = list.Where(r =>
                        searchQueryBits.Length == 0 || MeetsQuery(searchQueryBits, r)
                    )
                    .OrderByDescending(r => r.CreatedOnUTC)
                    .ToList();

                res.PageTotalItems = (uint)filtered.Count;

                // Pagination
                if (request.PageSize > 0)
                {
                    res.PageOffsetStart = request.PageOffset;
                    filtered = filtered
                        .Skip((int)request.PageOffset)
                        .Take((int)request.PageSize)
                        .ToList();
                }

                res.Records.AddRange(filtered);
                res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

                return res;
            } 
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return new();
            }
        }

        private bool IsValid(CreateAssetRequest request)
        {
            if (request == null)
                return false;

            switch (request.CreateAssetRequestOneofCase)
            {
                case CreateAssetRequest.CreateAssetRequestOneofOneofCase.Audio:
                    if (!IsValid(request.Audio))
                        return false;
                    break;
                case CreateAssetRequest.CreateAssetRequestOneofOneofCase.Image:
                    if (!IsValid(request.Image))
                        return false;
                    break;
                default:
                    return false;
            }

            return true;
        }

        private bool IsValid(AudioAssetData audio)
        {
            if (audio.Public == null)
                return false;
            if (audio.Private == null)
                audio.Private = new();

            return true;
        }

        private bool IsValid(ImageAssetData image)
        {
            if (image.Public == null)
                return false;
            if (image.Private == null)
                image.Private = new();

            return true;
        }

        private bool MeetsQuery(string[] searchQueryBits, AssetListRecord rec)
        {
            if (MeetsQuery(searchQueryBits, rec.Title.ToLower()))
                return true;

            if (MeetsQuery(searchQueryBits, rec.Caption.ToLower()))
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

        private static CreateAssetResponse CreateAssetError(APIErrorReason reason, string message) =>
            new() { Error = GenericErrorExtensions.CreateError(reason, message) };

        private static GetAssetResponse GetAssetError(APIErrorReason reason, string message) =>
            new() { Error = GenericErrorExtensions.CreateError(reason, message) };

        private static GetAssetAdminResponse GetAssetAdminError(APIErrorReason reason, string message) =>
            new() { Error = GenericErrorExtensions.CreateError(reason, message) };

        private static GetAssetByOldContentIDResponse GetAssetByOldContentIDError(
            APIErrorReason reason,
            string message
        ) => new() { Error = GenericErrorExtensions.CreateError(reason, message) };
    }
}
