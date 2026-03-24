using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Services;
using IT.WebServices.Clients;
using IT.WebServices.Clients.Authentication;
using IT.WebServices.Clients.Payments;
using IT.WebServices.Settings;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;
using IT.WebServices.Helpers;

namespace IT.WebServices.Authorization.Discord
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5000, o => o.Protocols = HttpProtocols.Http1AndHttp2);
            });

            if (builder.Environment.IsDevelopment())
                builder.Services.AddHostedService<DevTunnelService>();
            builder.Services.AddSingleton<ClientGrpcHelper>();
            builder.Services.AddSingleton<MySQLHelper>();
            builder.Services.AddSingleton<UserClient>();
            builder.Services.AddSingleton<PaymentClient>();
            builder.Services.AddDiscordData();
            builder.Services.AddSettingsHelpers();
            builder.Services.AddDiscordSettings(builder.Configuration);
            builder.Services.AddDiscordClasses();

            builder.Services.AddGrpc().AddJsonTranscoding();
            builder.Services.AddGrpcSwagger();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Discord API", Version = "v1" });
            });

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseSwagger();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Discord API v1");
                });
            }

            app.MapControllers();
            app.MapDiscordGrpcServices();
            app.Run();
        }
    }
}
