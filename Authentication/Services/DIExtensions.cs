using IT.WebServices.Authentication;
using IT.WebServices.Authentication.Services;
using IT.WebServices.Authentication.Services.Data;
using IT.WebServices.Authentication.Services.Helpers;
using IT.WebServices.AuditLog;
using IT.WebServices.Helpers;
using IT.WebServices.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics.CodeAnalysis;
using static Google.Rpc.Context.AttributeContext.Types;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddAuthenticationClasses(this IServiceCollection services)
        {
            services.AddSingleton<IProfilePicDataProvider, FileSystemProfilePicDataProvider>();
            services.AddSingleton<IUserDataProvider, SqlUserDataProvider>();

            services.AddSingleton<UserServiceInternal>();
            services.AddSingleton<IUserService, UserServiceInternal>();

            services.AddSingleton<ClaimsClient>();
            services.AddSingleton<MySQLHelper>();
            services.AddSingleton<OfflineHelper>();
            services.AddSingleton<TokenHelper>();
            services.AddSingleton<SignedQRHelper>();

            return services;
        }

        public static void MapAuthenticationGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<BackupService>();
            //endpoints.MapGrpcService<ServiceService>();
            endpoints.MapGrpcService<UserService>();
        }
    }
}
