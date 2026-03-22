using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Services;
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
