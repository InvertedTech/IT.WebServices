using IT.WebServices.Authorization.Discord;
using IT.WebServices.Authorization.Discord.Decorators;
using IT.WebServices.Authorization.Discord.Handlers;
using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Services;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDiscordClasses(this IServiceCollection services)
        {
            services.AddHttpClient<DiscordRestClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<DiscordSettings>>().Value;
                var uri = opts.DiscordUri ?? throw new NullReferenceException("DiscordUri MUST Be Set");
                client.BaseAddress = new Uri(uri.TrimEnd('/') + '/');
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", opts.BotToken ?? throw new NullReferenceException("BotToken MUST Be Set"));
            });

            foreach (var handlerType in Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<SlashCommandAttribute>() != null
                         && t.IsAssignableTo(typeof(ISlashCommandHandler))))
            {
                services.AddSingleton(handlerType);
            }

            services.AddSingleton<DiscordCommandRouter>();
            services.AddHostedService<DiscordCommandRegistrationService>();

            return services;
        }

        public static IEndpointRouteBuilder MapDiscordGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            return endpoints;
        }
    }
}
