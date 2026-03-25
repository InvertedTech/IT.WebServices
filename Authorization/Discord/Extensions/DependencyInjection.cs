using IT.WebServices.Authorization.Discord;
using IT.WebServices.Authorization.Discord.Data;
using IT.WebServices.Authorization.Discord.Decorators;
using IT.WebServices.Authorization.Discord.Handlers;
using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Services;
using IT.WebServices.Clients.Authentication;
using IT.WebServices.Helpers;
using IT.WebServices.Settings;
using System.Net.Http.Headers;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDiscordSettings(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<DiscordSettings>(sp =>
            {
                var client = sp.GetRequiredService<SettingsClient>();
                var settingsBuilder = new DiscordSettingsBuilder(client);
                return settingsBuilder.Build();
            });
            services.Configure<DiscordBotSettings>(
                    configuration.GetSection(DiscordBotSettings.SectionName));
            return services;
        }
        public static IServiceCollection AddDiscordClasses(this IServiceCollection services)
        {
            services.AddHttpClient<DiscordRestClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<DiscordSettings>();
                var uri = opts.DiscordUri ?? throw new NullReferenceException("DiscordUri MUST Be Set");
                client.BaseAddress = new Uri(uri.TrimEnd('/') + '/');
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", opts.BotToken ?? throw new NullReferenceException("BotToken MUST Be Set"));
            });

            // Scan For Slash Command Attribute
            var slashCommandHandlers = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<SlashCommandAttribute>() != null
                         && t.IsAssignableTo(typeof(ISlashCommandHandler)));

            services.AddSingleton<OfflineHelper>();
            services.AddSingleton<UserClient>();
            foreach (var handlerType in slashCommandHandlers)
            {
                services.AddSingleton(handlerType);
            }

            services.AddSingleton<DiscordCommandRouter>();
            services.AddHostedService<MetadataRegistrationService>();
            services.AddSingleton<DiscordService>();
            return services;
        }

        public static IServiceCollection AddDiscordData(this IServiceCollection services)
        {
            services.AddSingleton<IMemberDataProvider, SqlMemberDataProvider>();
            services.AddSingleton<IDiscordTicketDataProvider, SqlDiscordTicketDataProvider>();
            services.AddSingleton<IShunDataProvider, SqlDiscordShunDataProvider>();
            return services;
        }

        public static IEndpointRouteBuilder MapDiscordGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<DiscordService>();
            return endpoints;
        }
    }
}
