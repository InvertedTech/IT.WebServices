using IT.WebServices.Notification;
using IT.WebServices.Notification.Services.Clients;
using IT.WebServices.Notification.Services.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddNotificationClasses(this IServiceCollection services)
        {
            services.AddSingleton<INotificationServiceInternal, NotificationServiceInternal>();
            services.AddSingleton<SendgridClient>();

            return services;
        }

        public static void MapNotificationGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<NotificationService>();
        }
    }
}
