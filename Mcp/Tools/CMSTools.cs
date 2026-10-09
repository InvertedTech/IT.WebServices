using IT.WebServices.Authentication;
using IT.WebServices.Clients.CMS;
using IT.WebServices.Fragments.Content;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Mcp.Tools
{
    public class CMSTools
    {
        private readonly ONUserHelper _userHelper;
        private readonly ContentClient _content;

        public CMSTools(ONUserHelper userHelper, ContentClient content)
        {
            _userHelper = userHelper;
            _content = content;
        }

        [McpServerTool(Name = "ListContent")]
        [Description("Lists the content availible in the CMS.")]
        public async Task<List<ContentListRecord>> ListContentAsync(
               [Description("The number of content items to retrieve per page.")]
                int pageSize = 25,
                [Description("The starting offset for pagination.")]
                int pageOffsetStart = 0
            )
        {
            // TODO: Handle Errors
            var req = new GetAllContentAdminRequest
            {
                PageSize = (uint)pageSize,
                PageOffset = (uint)pageOffsetStart
            };

            var res = await _content.GetAllContentAdmin(req);
            return res.Records.ToList();
        }

        [McpServerTool(Name = "SearchContent")]
        [Description("Searches for content items based on a query.")]
        public async Task<List<ContentListRecord>> SearchContent(
                [Description("The number of content items to retrieve per page.")]
                int pageSize = 25,
                [Description("The starting offset for pagination.")]
                int pageOffsetStart = 0,
                string? query = null
            )
        {
            // TODO: Handle Errors
            var req = new SearchContentRequest
            {
                PageSize = (uint)pageSize,
                PageOffset = (uint)pageOffsetStart,
                Query = query ?? string.Empty
            };

            var res = await _content.Search(req);
            return res.Records.ToList();
        }

        [McpServerTool(Name = "GetContent")]
        [Description("Gets a specific content item by it's ID.")]
        public async Task<ContentRecord> GetContentAsync(
            [Description("The ID of the content item to retrieve.")]
            string id)
        {
            // TODO: Handle Errors
            var req = new GetContentAdminRequest
            {
                ContentID = id
            };

            var res = await _content.GetContentAdmin(req);
            return res.Record;
        }
    }
}
