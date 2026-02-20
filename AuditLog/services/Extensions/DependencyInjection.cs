using IT.WebServices.AuditLog.Services;
using IT.WebServices.AuditLog.Services.Data;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAuditLogClasses(this IServiceCollection services)
        {
            services.AddSingleton<MySQLHelper>();
            services.AddSingleton<OfflineHelper>();
            services.AddSingleton<IAuditLogDataProvider, FileSystemAuditLogDataProvider>();
            return services;
        }

        public static void MapAuditLogGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<AuditLogService>();
        }
    }
}
