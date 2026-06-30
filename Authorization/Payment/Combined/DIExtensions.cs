using IT.WebServices.Authorization;
using IT.WebServices.Authorization.Payment.Combined.Helpers;
using IT.WebServices.Authorization.Payment.Combined.Helpers.BulkJobs;
using IT.WebServices.Authorization.Payment.Combined.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddPaymentClasses(this IServiceCollection services)
        {
            services.AddManualPaymentClasses();
            services.AddFortisClasses();
            services.AddPaypalClasses();
            services.AddStripeClasses();
            services.AddTaxClasses();

            services.AddSingleton<ClaimsServiceInternal>();
            services.AddSingleton<IClaimsProvider, ClaimsServiceInternal>();

            services.AddSingleton<BulkHelper>();
            services.AddSingleton<ReconcileHelper>();

            services.AddTransient<LookForMissingSubscriptions>();
            services.AddTransient<LookForNewPaymentsOneDay>();
            services.AddTransient<LookForNewPaymentsOneMonth>();
            services.AddTransient<LookForNewPaymentsOneWeek>();
            services.AddTransient<ReconcileAll>();
            services.AddTransient<ReconcileStripeTaxRates>();

            return services;
        }

        public static void MapPaymentGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapManualPaymentGrpcServices();
            endpoints.MapFortisGrpcServices();
            endpoints.MapPaypalGrpcServices();
            endpoints.MapStripeGrpcServices();
            endpoints.MapTaxGrpcServices();

            endpoints.MapGrpcService<AdminPaymentService>();
            endpoints.MapGrpcService<BackupService>();
            endpoints.MapGrpcService<ClaimsService>();
            endpoints.MapGrpcService<PaymentService>();
            endpoints.MapGrpcService<ServiceOpsService>();
        }
    }
}
