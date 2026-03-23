using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Services;
using IT.WebServices.Clients;
using IT.WebServices.Clients.Authentication;
using IT.WebServices.Clients.Payments;
using IT.WebServices.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace IT.WebServices.Authorization.Discord
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            if (builder.Environment.IsDevelopment())
                builder.Services.AddHostedService<DevTunnelService>();
            builder.Services.AddSingleton<ClientGrpcHelper>();
            builder.Services.AddSingleton<UserClient>();
            builder.Services.AddSingleton<PaymentClient>();
            builder.Services.AddSettingsHelpers();
            builder.Services.AddDiscordSettings(builder.Configuration);
            builder.Services.AddDiscordClasses();

            builder.Services.AddControllers();

            var app = builder.Build();
            app.MapControllers();
            app.Run();
        }
    }
}
