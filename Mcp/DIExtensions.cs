using IT.WebServices.Clients.CMS;
using IT.WebServices.Fragments.Comment;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Mcp;
using IT.WebServices.Mcp.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddMcpClasses(this IServiceCollection services)
        {
            services.AddScoped<McpSettings>();

            // Combined never calls AddGrpcClientClasses, so register just what the tools need.
            // ClientGrpcHelper is already registered by AddAuthenticationClasses.
            services.AddGrpcClient<ContentInterface.ContentInterfaceClient>();
            services.AddGrpcClient<CommentInterface.CommentInterfaceClient>();
            services.AddScoped<ContentClient>();
            services.AddScoped<CommentClient>();

            services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<CMSTools>()
                .WithTools<CommentTools>();

            return services;
        }

        public static IApplicationBuilder UseMcpGate(this IApplicationBuilder app)
        {
            return app.UseWhen(
                ctx => ctx.Request.Path.StartsWithSegments("/mcp"),
                branch => branch.UseMiddleware<McpGateMiddleware>());
        }

        public static void MapMcpServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapMcp("/mcp").RequireAuthorization();
        }
    }
}
