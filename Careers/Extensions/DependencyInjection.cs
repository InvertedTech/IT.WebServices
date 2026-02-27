using IT.WebServices.Careers;
using IT.WebServices.Careers.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddCareersClasses(this IServiceCollection services)
        {
            services.AddSingleton<ICareersDataProvider, FileSystemCareersDataProvider>();

            return services;
        }

        public static void MapCareersGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<CareersService>();
        }
    }
}
