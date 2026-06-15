using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Careers;
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

        public async Task<CreateContentResponse> CreateContent(CreateContentRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.CreateContentAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in CreateContent");
                return new();
            }
        }

        public async Task<ModifyContentResponse> ModifyContent(ModifyContentRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.ModifyContentAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in ModifyContent");
                return new();
            }
        }

        public async Task<PublishContentResponse> PublishContent(PublishContentRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.PublishContentAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in PublishContent");
                return new();
            }
        }

        public async Task<UnpublishContentResponse> UnpublishContent(UnpublishContentRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.UnpublishContentAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in UnPublishContent");
                return new();
            }
        }

        public async Task<GetAllContentAdminResponse> GetAllContentAdmin(GetAllContentAdminRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetAllContentAdminAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in GetAllContentAdmin");
                return new();
            }
        }

        public async Task<GetContentAdminResponse> GetContentAdmin(GetContentAdminRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetContentAdminAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {

                log.LogError(ex, "Error in GetContentAdmin");
                return new();
            }
        }

        public async Task<DeleteContentResponse> DeleteContent(DeleteContentRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.DeleteContentAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {

                log.LogError(ex, "Error in DeleteContent");
                return new();
            }
        }

        public async Task<UndeleteContentResponse> UnDeleteContent(UndeleteContentRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.UndeleteContentAsync(req, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {

                log.LogError(ex, "Error in DeleteContent");
                return new();
            }
        }
    }
}
