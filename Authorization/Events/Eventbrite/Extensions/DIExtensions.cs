using IT.WebServices.Authorization.Events.Eventbrite;
using IT.WebServices.Authorization.Events.Generic;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddEventbriteClasses(this IServiceCollection services)
        {
            services.AddSingleton<IGenericEventProvider, EventbriteGenericEventProvider>();

            return services;
        }
    }
}
