using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.CMS
{
    public class AssetClient
    {
        private readonly AssetInterface.AssetInterfaceClient client;
        private readonly ONUserHelper userHelper;
        private readonly ILogger log;

        public AssetClient(AssetInterface.AssetInterfaceClient client, ONUserHelper userHelper, ILogger<AssetClient> log)
        {
            this.client = client;
            this.userHelper = userHelper;
            this.log = log;
        }

        public async Task<ImageAssetRecord?> Create(CreateAssetRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.CreateAssetAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res.Record.Image;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in CreateAsset");
                return null;
            }
        }

        public async Task<GetAssetResponse?> Get(GetAssetRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetAssetAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in Get");

                return new()
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "Unknown Error"
                    )
                };
            }
        }

        public async Task<GetAssetAdminResponse?> GetAdmin(GetAssetAdminRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetAssetAdminAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetAdmin");

                return new()
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "Unknown Error"
                    )
                };
            }
        }

        public async Task<SearchAssetResponse?> SearchAsset(SearchAssetRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.SearchAssetAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in SearchAsset");

                return new()
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "Unknown Error"
                    )
                };
            }
        }
    }
}
