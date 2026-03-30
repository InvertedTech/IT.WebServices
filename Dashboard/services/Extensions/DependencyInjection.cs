using IT.WebServices.Dashboard.Services;
using IT.WebServices.Dashboard.Services.Data;
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
            services.AddSingleton<IUserDataProvider, SqlUserDataProvider>();
            services.AddSingleton<ISubscriptionDataProvider, SqlSubscriptionDataProvider>();
            return services;
        }

        public static void MapDashboardGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<DashboardService>();
        }
    }
}
