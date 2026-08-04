using IT.WebServices.Authorization.Events.Combined.Services;
using IT.WebServices.Authorization.Events.Generic.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddEventClasses(this IServiceCollection services)
        {
            services.AddSingleton<IGenericEventRecordProvider, FileSystemEventRecordProvider>();
            services.AddSingleton<IGenericEventTicketRecordProvider, FileSystemEventTicketRecordProvider>();

            return services;
        }

        public static void MapEventGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<EventService>();
            endpoints.MapGrpcService<AdminEventService>();
        }
    }
}
