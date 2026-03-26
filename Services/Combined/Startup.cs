using System;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Events.Extensions;
using IT.WebServices.Models;
using IT.WebServices.Services.Combined.Models;
using IT.WebServices.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using AuthS = IT.WebServices.Authentication.Services;
using CmsS = IT.WebServices.Content.CMS.Services;

namespace IT.WebServices.Services.Combined
{
    public class Startup
    {
        private static byte[] PONG_RESPONSE = { (byte)'p', (byte)'o', (byte)'n', (byte)'g' };

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(sp => sp);

            services
                .AddControllersWithViews()
                //.AddApplicationPart(typeof(UserApiController).GetTypeInfo().Assembly)
                //.AddApplicationPart(typeof(AssetApiController).GetTypeInfo().Assembly)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = new NeutralNamingPolicy();
                });
            ;

            services.AddGrpc(opt =>
            {
                opt.EnableDetailedErrors = true;
                opt.MaxReceiveMessageSize = int.MaxValue;
                opt.MaxSendMessageSize = int.MaxValue;
            });

            //services.AddGrpcHttpApi();
            //services.AddGrpcReflection();

            var securityScheme = new OpenApiSecurityScheme()
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JSON Web Token based security",
            };

            var securityReq = new OpenApiSecurityRequirement()
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    new string[] { }
                },
            };

            var info = new OpenApiInfo()
            {
                Version = "v1",
                Title = "IT.WebServices API",
                Description = "IT.WebServices API",
            };

            services.AddSwaggerGen(o =>
            {
                o.SwaggerDoc("v1", info);
                o.AddSecurityDefinition("Bearer", securityScheme);
                o.AddSecurityRequirement(securityReq);
            });

            services.AddGrpcSwagger();

            services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));

            services.AddJwtAuthentication();
            services.AddAuditLogClasses();
            services.AddAuthenticationClasses();
            services.AddCMSClasses();
            services.AddCommentClasses();
            services.AddSettingsClasses();
            services.AddStatsClasses();
            services.AddEventsClasses();
            services.AddPaymentClasses();
            services.AddDashboardClasses();
            services.AddCareersClasses();
            services.AddNotificationClasses();
            services.AddMerchClasses();

            CryptoProviderFactory.DefaultCacheSignatureProviders = false;

            Console.WriteLine("*** Loading pubkey: (" + JwtExtensions.GetPublicKey() + ")  ***");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.Map(
                "/ping",
                (app1) =>
                    app1.Run(async context =>
                    {
                        await context.Response.BodyWriter.WriteAsync(PONG_RESPONSE);
                    })
            );

            app.UseStaticFiles();

            app.UseSwagger();
            app.UseSwaggerUI();

            if (env.IsDevelopment())
                Program.IsDevelopment = true;

            app.UseRouting();

            app.UseJwtApiAuthentication();
            app.LoadStatsSubscriptions();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAuditLogGrpcServices();
                endpoints.MapAuthenticationGrpcServices();
                endpoints.MapCMSGrpcServices();
                endpoints.MapCommentGrpcServices();
                endpoints.MapSettingsGrpcServices();
                endpoints.MapStatsGrpcServices();
                endpoints.MapEventsGrpcServices();

                endpoints.MapPaymentGrpcServices();
                endpoints.MapDashboardGrpcServices();
                endpoints.MapCareersGrpcServices();
                endpoints.MapNotificationGrpcServices();
                endpoints.MapMerchGrpcServices();
                //endpoints.MapGrpcReflectionService();

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}"
                );
            });
        }
    }
}
