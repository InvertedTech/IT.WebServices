using IT.WebServices.Authorization.Payment.Fortis;
using IT.WebServices.Authorization.Payment.Fortis.Clients;
using IT.WebServices.Authorization.Payment.Fortis.Helpers;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddFortisClasses(this IServiceCollection services)
        {
            services.AddPaymentBaseClasses();

            services.AddSingleton<SettingsHelper>();

            services.AddSingleton<FortisClient>();
            services.AddSingleton<FortisContactHelper>();
            services.AddSingleton<FortisSubscriptionHelper>();
            services.AddSingleton<FortisTokenHelper>();
            services.AddSingleton<FortisTransactionHelper>();

            services.AddSingleton<IGenericPaymentProcessor, FortisGenericPaymentProcessor>();

            return services;
        }

        public static void MapFortisGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<FortisService>();
        }
    }
}
