using IT.WebServices.Fragments.Merch;
using IT.WebServices.Fragments.Merch.Shopify;
using IT.WebServices.Helpers;
using IT.WebServices.Merch.Generic.Data;
using IT.WebServices.Merch.Jobs;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Merch.Shopify.Jobs
{
    public class PullFromShopifyProcessor : IPullFromAllProcessor
    {
        private readonly IGenericMerchRecordProvider recordProvider;
        private readonly SettingsHelper settingsClient;
        private readonly ILogger log;

        public PullFromShopifyProcessor(IGenericMerchRecordProvider recordProvider, SettingsHelper settingsClient, ILogger<PullFromShopifyProcessor> log)
        {
            this.recordProvider = recordProvider;
            this.settingsClient = settingsClient;
            this.log = log;
        }

        public async Task Run(MerchBulkActionProgress progress, CancellationToken cancellationToken)
        {
            if (!(settingsClient.Public.Merch?.Shopify?.IsEnabled ?? false))
                return;

            var stores = settingsClient.Owner.Merch?.Shopify?.Stores?.ToArray() ?? Array.Empty<ShopifyStoreConfig>();

            var numRuns = 100;

            for (int i = 0; i < numRuns; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                progress.Progress = 1.0F * i / numRuns;
                progress.StatusMessage = $"Pulling from Shopify {i}";

                await Task.Delay(10000, cancellationToken);
            }
        }
    }
}
