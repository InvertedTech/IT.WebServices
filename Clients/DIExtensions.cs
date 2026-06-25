using IT.WebServices.Clients;
using IT.WebServices.Clients.CMS;
using IT.WebServices.Clients.Merch;
using IT.WebServices.Clients.Payment;
using IT.WebServices.Clients.Settings;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Fortis;
using IT.WebServices.Fragments.Authorization.Payment.Manual;
using IT.WebServices.Fragments.Authorization.Payment.Paypal;
using IT.WebServices.Fragments.Authorization.Payment.Stripe;
using IT.WebServices.Fragments.Careers;
using IT.WebServices.Fragments.Comment;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Content.Stats;
using IT.WebServices.Fragments.Dashboard;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Fragments.Notification;
using IT.WebServices.Fragments.Page;
using IT.WebServices.Fragments.Settings;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddGrpcClientClasses(this IServiceCollection services)
        {
            services.AddScoped<SettingsClient>();

            services.AddSingleton<ClientGrpcHelper>();
            services.AddSingleton<PublicSettingsClient>();

            services.AddSingleton<CategoryHelper>();
            services.AddSingleton<ChannelHelper>();
            services.AddSingleton<SubscriptionTierHelper>();

            services.AddGrpcClient<AssetInterface.AssetInterfaceClient>();
            services.AddGrpcClient<CareersInterface.CareersInterfaceClient>();
            services.AddGrpcClient<CommentInterface.CommentInterfaceClient>();
            services.AddGrpcClient<ContentInterface.ContentInterfaceClient>();
            services.AddGrpcClient<DashboardInterface.DashboardInterfaceClient>();
            services.AddGrpcClient<FortisInterface.FortisInterfaceClient>();
            services.AddGrpcClient<ManualPaymentInterface.ManualPaymentInterfaceClient>();
            services.AddGrpcClient<MerchInterface.MerchInterfaceClient>();
            services.AddGrpcClient<NotificationInterface.NotificationInterfaceClient>();
            services.AddGrpcClient<PageInterface.PageInterfaceClient>();
            services.AddGrpcClient<PaymentInterface.PaymentInterfaceClient>();
            services.AddGrpcClient<AdminPaymentInterface.AdminPaymentInterfaceClient>();
            services.AddGrpcClient<PaypalInterface.PaypalInterfaceClient>();
            services.AddGrpcClient<SettingsInterface.SettingsInterfaceClient>();
            services.AddGrpcClient<StatsLikeInterface.StatsLikeInterfaceClient>();
            services.AddGrpcClient<StatsQueryInterface.StatsQueryInterfaceClient>();
            services.AddGrpcClient<StatsProgressInterface.StatsProgressInterfaceClient>();
            services.AddGrpcClient<StatsSaveInterface.StatsSaveInterfaceClient>();
            services.AddGrpcClient<StatsShareInterface.StatsShareInterfaceClient>();
            services.AddGrpcClient<StatsViewInterface.StatsViewInterfaceClient>();
            services.AddGrpcClient<StripeInterface.StripeInterfaceClient>();
            services.AddGrpcClient<UserInterface.UserInterfaceClient>();

            services.AddScoped<PaymentClient>();
            services.AddScoped<AssetClient>();
            services.AddScoped<ContentClient>();
            services.AddScoped<MerchClient>();

            return services;
        }

        public static IServiceCollection AddGrpcClient<TService>(this IServiceCollection services) where TService : class
        {
            services.AddSingleton(sp =>
            {
                var helper = sp.GetRequiredService<ClientGrpcHelper>();

                var service = Activator.CreateInstance(typeof(TService), helper.CombinedServiceChannel);
                if (service is null)
                    throw new Exception("Error in AddGrpcClient - Activator.CreateInstance returned null");

                return (TService)service;
            });

            return services;
        }

    }
}
