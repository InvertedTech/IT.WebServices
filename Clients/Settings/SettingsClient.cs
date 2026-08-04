using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Clients.CMS;
using IT.WebServices.Fragments.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Settings
{
    public class SettingsClient
    {
        private readonly SettingsInterface.SettingsInterfaceClient client;
        private readonly PublicSettingsClient publicSettingsClient;
        private readonly ONUserHelper userHelper;
        private readonly ILogger log;

        private Lazy<Task<SettingsPrivateData>> lazyPrivateData;
        public Task<SettingsPrivateData> PrivateData => lazyPrivateData.Value;

        private Lazy<Task<SettingsOwnerData>> lazyOwnerData;
        public Task<SettingsOwnerData> OwnerData => lazyOwnerData.Value;

        public SettingsClient(SettingsInterface.SettingsInterfaceClient client, PublicSettingsClient publicSettingsClient, ONUserHelper userHelper, ILogger<SettingsClient> log)
        {
            this.client = client;
            this.publicSettingsClient = publicSettingsClient;
            this.userHelper = userHelper;
            this.log = log;

            lazyPrivateData = new Lazy<Task<SettingsPrivateData>>(FetchPrivateData);
            lazyOwnerData = new Lazy<Task<SettingsOwnerData>>(FetchOwnerData);
        }

        public void InvalidateCache()
        {
            publicSettingsClient.InvalidateCache();

            lazyPrivateData = new Lazy<Task<SettingsPrivateData>>(FetchPrivateData);
            lazyOwnerData = new Lazy<Task<SettingsOwnerData>>(FetchOwnerData);
        }

        public async Task<Fragments.APIError> ModifyCMSOwnerSettings(ModifyCMSOwnerDataRequest req)
        {
            var res = await client.ModifyCMSOwnerDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyCMSPrivateSettings(ModifyCMSPrivateDataRequest req)
        {
            var res = await client.ModifyCMSPrivateDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyCMSPublicSettings(ModifyCMSPublicDataRequest req)
        {
            var res = await client.ModifyCMSPublicDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyCommentsOwnerSettings(ModifyCommentsOwnerDataRequest req)
        {
            var res = await client.ModifyCommentsOwnerDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyCommentsPrivateSettings(ModifyCommentsPrivateDataRequest req)
        {
            var res = await client.ModifyCommentsPrivateDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyCommentsPublicSettings(ModifyCommentsPublicDataRequest req)
        {
            var res = await client.ModifyCommentsPublicDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyEventOwnerSettings(ModifyEventOwnerSettingsRequest req)
        {
            var res = await client.ModifyEventOwnerSettingsAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyEventPrivateSettings(ModifyEventPrivateSettingsRequest req)
        {
            var res = await client.ModifyEventPrivateSettingsAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyEventPublicSettings(ModifyEventPublicSettingsRequest req)
        {
            var res = await client.ModifyEventPublicSettingsAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyMerchOwnerSettings(ModifyMerchOwnerSettingsRequest req)
        {
            var res = await client.ModifyMerchOwnerSettingsAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyMerchPublicSettings(ModifyMerchPublicSettingsRequest req)
        {
            var res = await client.ModifyMerchPublicSettingsAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyNotificationOwnerSettings(ModifyNotificationOwnerDataRequest req)
        {
            var res = await client.ModifyNotificationOwnerDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyNotificationPrivateSettings(ModifyNotificationPrivateDataRequest req)
        {
            var res = await client.ModifyNotificationPrivateDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyNotificationPublicSettings(ModifyNotificationPublicDataRequest req)
        {
            var res = await client.ModifyNotificationPublicDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyPersonalizationOwnerSettings(ModifyPersonalizationOwnerDataRequest req)
        {
            var res = await client.ModifyPersonalizationOwnerDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyPersonalizationPrivateSettings(ModifyPersonalizationPrivateDataRequest req)
        {
            var res = await client.ModifyPersonalizationPrivateDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifyPersonalizationPublicSettings(ModifyPersonalizationPublicDataRequest req)
        {
            var res = await client.ModifyPersonalizationPublicDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifySubscriptionOwnerSettings(ModifySubscriptionOwnerDataRequest req)
        {
            var res = await client.ModifySubscriptionOwnerDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifySubscriptionPrivateSettings(ModifySubscriptionPrivateDataRequest req)
        {
            var res = await client.ModifySubscriptionPrivateDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        public async Task<Fragments.APIError> ModifySubscriptionPublicSettings(ModifySubscriptionPublicDataRequest req)
        {
            var res = await client.ModifySubscriptionPublicDataAsync(req, userHelper.GetGrpcCallOptions());

            InvalidateCache();

            return res.Error;
        }

        private async Task<SettingsPrivateData> FetchPrivateData()
        {
            var res = await client.GetOwnerDataAsync(new(), userHelper.GetGrpcCallOptions());

            return res.Private;
        }

        private async Task<SettingsOwnerData> FetchOwnerData()
        {
            var res = await client.GetOwnerDataAsync(new(), userHelper.GetGrpcCallOptions());

            return res.Owner;
        }
    }
}
