using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Comment;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.CMS
{
    public class CommentClient
    {
        private readonly CommentInterface.CommentInterfaceClient client;
        private readonly ONUserHelper userHelper;
        private readonly ILogger log;

        public CommentClient(CommentInterface.CommentInterfaceClient client, ONUserHelper userHelper, ILogger<CommentClient> log)
        {
            this.client = client;
            this.userHelper = userHelper;
            this.log = log;
        }

        public async Task<GetCommentsResponse> GetCommentsForContent(GetCommentsForContentRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetCommentsForContentAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetCommentsForContent");
                return new();
            }
        }

        public async Task<GetCommentsResponse> GetCommentsForComment(GetCommentsForCommentRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetCommentsForCommentAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));
                return res;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetCommentsForComment");
                return new();
            }
        }
    }
}
