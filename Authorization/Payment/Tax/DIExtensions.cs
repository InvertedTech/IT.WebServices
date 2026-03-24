using IT.WebServices.Authorization.Payment.Tax.Data;
using IT.WebServices.Authorization.Payment.Tax.Services;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddTaxClasses(this IServiceCollection services)
        {
            services.AddSingleton<MySQLHelper>();

            services.AddSingleton<TaxServiceInternal>();
            services.AddSingleton<ISalesTaxRecordProvider, SqlSalesTaxRecordProvider>();

            return services;
        }

        public static void MapTaxGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<BackupService>();
        }
    }
}
