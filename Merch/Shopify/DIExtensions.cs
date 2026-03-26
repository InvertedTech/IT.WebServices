using IT.WebServices.Helpers;
using IT.WebServices.Merch.Jobs;
using IT.WebServices.Merch.Shopify.Jobs;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddShopifyClasses(this IServiceCollection services)
        {
            services.AddMerchBaseClasses();

            services.AddSingleton<SettingsHelper>();

            services.AddSingleton<IPullFromAllProcessor, PullFromShopifyProcessor>();

            return services;
        }

        public static void MapShopifyGrpcServices(this IEndpointRouteBuilder endpoints)
        {
        }
    }
}
