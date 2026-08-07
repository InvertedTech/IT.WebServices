using IT.WebServices.Authorization.Events.Eventbrite;
using IT.WebServices.Authorization.Events.Eventbrite.Services;
using IT.WebServices.Authorization.Events.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddEventbriteClasses(this IServiceCollection services)
        {
            services.AddSingleton<EventbriteClient>();
            services.AddSingleton<IGenericEventProvider, EventbriteGenericEventProvider>();

            return services;
        }

        public static void MapEventbriteGrpcService(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<EventbriteService>();
        }
    }
}
