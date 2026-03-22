using IT.WebServices.Authorization.Discord.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IT.WebServices.Authorization.Discord
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Discord service");
            var builder = WebApplication.CreateBuilder(args);

            if (builder.Environment.IsDevelopment())
                builder.Services.AddHostedService<DevTunnelService>();

            builder.Services.AddSettingsHelpers();
            // TODO: Replace With A Factory  Or Something
            builder.Services.Configure<DiscordSettings>(
                    builder.Configuration.GetSection(DiscordSettings.SectionName));
            builder.Services.Configure<DiscordBotSettings>(
                    builder.Configuration.GetSection(DiscordBotSettings.SectionName));

            builder.Services.AddDiscordClasses();

            builder.Services.AddControllers();

            var app = builder.Build();
            app.MapControllers();
            app.Run();
        }
    }
}
