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
            services.AddHttpClient<EventbriteClient>(c =>
            {
                c.BaseAddress = new Uri("https://www.eventbriteapi.com/v3/");
                c.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddSingleton<IGenericEventProvider, EventbriteGenericEventProvider>();

            return services;
        }

        public static void MapEventbriteGrpcService(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<EventbriteService>();
        }
    }
}
