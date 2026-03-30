using IT.WebServices.Authorization;
using IT.WebServices.Merch.Combined.Helpers;
using IT.WebServices.Merch.Combined.Helpers.BulkJobs;
using IT.WebServices.Merch.Combined.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddMerchClasses(this IServiceCollection services)
        {
            services.AddShopifyClasses();

            services.AddSingleton<BulkHelper>();

            services.AddTransient<PullFromAll>();

            return services;
        }

        public static void MapMerchGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapShopifyGrpcServices();

            endpoints.MapGrpcService<AdminMerchService>();
            endpoints.MapGrpcService<MerchService>();
        }
    }
}
