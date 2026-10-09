using IT.WebServices.Mcp;
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
            services
                .AddMcpServer()
                .WithHttpTransport();

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
