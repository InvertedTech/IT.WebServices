using IT.WebServices.Helpers;
using IT.WebServices.Merch.Jobs;
using IT.WebServices.Merch.Shopify.Jobs;
using Microsoft.AspNetCore.Routing;
using ShopifySharp;
using ShopifySharp.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddShopifyClasses(this IServiceCollection services)
        {
            services.AddMerchBaseClasses();

            services.AddHttpClient();
            services.AddSingleton<SettingsHelper>();
            services.AddShopifySharpServiceFactories();
            services.AddShopifySharpUtilities();
            services.AddShopifySharp<LeakyBucketExecutionPolicy>();

            services.AddSingleton<IPullFromAllProcessor, PullFromShopifyProcessor>();

            return services;
        }

        public static void MapShopifyGrpcServices(this IEndpointRouteBuilder endpoints)
        {
        }
    }
}
