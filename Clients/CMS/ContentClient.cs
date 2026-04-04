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
    public class ContentClient
    {
        private readonly ContentInterface.ContentInterfaceClient client;
        private readonly ONUserHelper userHelper;
        private readonly ILogger log;

        public ContentClient(ContentInterface.ContentInterfaceClient client, ONUserHelper userHelper, ILogger<ContentClient> log)
        {
            this.client = client;
            this.userHelper = userHelper;
            this.log = log;
        }

        public async Task<GetAllContentResponse> GetAll(GetAllContentRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetAllContentAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetAll");
                return new();
            }
        }

        public async Task<GetAllContentAdminResponse> GetAllAdmin(GetAllContentAdminRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetAllContentAdminAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetAllAdmin");
                return new();
            }
        }

        public async Task<SearchContentResponse> Search(SearchContentRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.SearchContentAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in Search");
                return new();
            }
        }
    }
}
