using Grpc.Core;
using IT.WebServices.Fragments.Content;
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
        private ClientGrpcHelper nameHelper;
        private ILogger log;

        public AssetClient(ClientGrpcHelper nameHelper, ILogger<AssetClient> log)
        {
            this.nameHelper = nameHelper;
            this.log = log;
        }

        public async Task<ImageAssetRecord?> SaveAsset(CreateAssetRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = new AssetInterface.AssetInterfaceClient(nameHelper.ContentServiceChannel);
                var options = new CallOptions(GetMetadata(), cancellationToken: cancellationToken);
                var res = await client.CreateAssetAsync(request, options);
                if (res.Error is not null)
                {
                    log.LogError(res.Error.Message, res.Error);
                    return null;
                }

                return res.Record.Image;
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message, ex);
                return null;
            }
        }
        private Metadata GetMetadata()
        {
            var data = new Metadata();
            data.Add("Authorization", "Bearer " + nameHelper.ServiceToken.Value);

            return data;
        }
    }
}
