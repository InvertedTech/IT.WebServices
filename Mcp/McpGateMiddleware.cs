using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace IT.WebServices.Mcp
{
    /// <summary>Rejects every /mcp request with 503 while the master switch is off.</summary>
    public class McpGateMiddleware
    {
        private readonly RequestDelegate next;

        public McpGateMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context, McpSettings settings)
        {
            if (!await settings.IsEnabledAsync())
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("MCP is disabled.");
                return;
            }

            await next(context);
        }
    }
}
