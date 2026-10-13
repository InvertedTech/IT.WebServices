using IT.WebServices.Authentication;
using IT.WebServices.Clients.CMS;
using IT.WebServices.Fragments.Comment;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Mcp.Tools
{
    public class CommentTools
    {
        private readonly CommentClient _comments;

        public CommentTools(CommentClient comments)
        {
            _comments = comments;
        }

        [McpServerTool(Name = "GetCommentsForContent")]
        [Description("Retrieves comments associated with a specific content item.")]
        public async Task<List<CommentResponseRecord>> GetCommentsForContentAsync(
                string contentId,
                int pageSize = 25,
                int pageOffsetStart = 0
            )
        {
            var req = new GetCommentsForContentRequest
            {
                ContentID = contentId,
                PageSize = (uint)pageSize,
                PageOffset = (uint)pageOffsetStart
            };
            var res = await _comments.GetCommentsForContent(req);
            return res.Records.ToList();
        }

        [McpServerTool(Name = "GetCommentsForComment")]
        [Description("Gets replies to a specific comment")]
        public async Task<List<CommentResponseRecord>> GetCommentsForCommentAsync(
                string commentId,
                int pageSize = 25,
                int pageOffsetStart = 0
            )
        {
            var req = new GetCommentsForCommentRequest
            {
                ParentCommentID = commentId,
                PageSize = (uint)pageSize,
                PageOffset = (uint)pageOffsetStart
            };
            var res = await _comments.GetCommentsForComment(req);
            return res.Records.ToList();
        }
    }
}
