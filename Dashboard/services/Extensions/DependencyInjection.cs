using IT.WebServices.Dashboard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddDashboardClasses(this IServiceCollection services)
        {
            services.AddSingleton<IKpiMockDataProvider, KpiMockDataProvider>();
            return services;
        }

        public static void MapDashboardGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<DashboardService>();
        }
    }
}
