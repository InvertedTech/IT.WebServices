using FortisAPI.Standard.Controllers;
using FortisAPI.Standard.Exceptions;
using FortisAPI.Standard.Models;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Tax.Services;
using IT.WebServices.Fragments.Authorization.Payment.Fortis;
using IT.WebServices.Helpers;
using IT.WebServices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IT.WebServices.Authorization.Payment.Fortis.Clients
{
    public class FortisClient
    {
        private readonly SettingsHelper settingsHelper;
        private readonly AppSettings appSettings;
        private readonly ILogger logger;
        private readonly SettingsHelper settingsClient;
        private readonly TaxServiceInternal taxService;

        public readonly FortisAPI.Standard.FortisAPIClient Client;

        public FortisClient(SettingsHelper settingsHelper, IOptions<AppSettings> appSettings, ILogger<FortisClient> logger, SettingsHelper settingsClient, TaxServiceInternal taxService)
        {
            this.settingsHelper = settingsHelper;
            this.appSettings = appSettings.Value;
            this.logger = logger;
            this.settingsClient = settingsClient;
            this.taxService = taxService;

            Client = GetClient();
        }

        public bool IsEnabled => settingsClient.Public?.Subscription?.Stripe?.Enabled ?? false && IsSettingsValid;
        private bool IsSettingsValid => settingsClient.Owner?.Subscription?.Stripe?.IsValid() ?? false;

        private FortisAPI.Standard.FortisAPIClient GetClient()
        {
            FortisAPI.Standard.FortisAPIClient client = new FortisAPI.Standard.FortisAPIClient.Builder()
                .CustomHeaderAuthenticationCredentials(settingsHelper.Owner.Subscription.Fortis.UserID, settingsHelper.Owner.Subscription.Fortis.UserApiKey, appSettings.FortisDeveloperId)
                .Environment(settingsHelper.Public.Subscription.Fortis.IsTest ? FortisAPI.Standard.Environment.Sandbox : FortisAPI.Standard.Environment.Production)
                .HttpClientConfig(config => config.NumberOfRetries(0))
                .Build();

            return client;
        }

        public async Task<FortisNewDetails?> GetNewDetails(uint amountCents, string postalCode, ONUser userToken, string successUrl, string cancelUrl, CancellationToken cancellationToken)
        {
            if (!IsEnabled)
                return null;

            var taxRecord = await taxService.Get("US", postalCode);
            var taxCents = taxRecord is null ? 0 : (int)taxRecord.CalculateTax(amountCents);
            var totalCents = amountCents + taxCents;

            ElementsController elementsController = Client.ElementsController;
            var body = new V1ElementsTransactionIntentionRequest()
            {
                Action = ActionEnum.Sale,
                Amount = (int)totalCents,
                TaxAmount = taxCents,
                Methods = new(),
                LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
            };
            body.Methods.Add(new(TypeEnum.Cc, settingsHelper.Owner.Subscription.Fortis.ProductID));

            try
            {
                ResponseTransactionIntention result = await elementsController.TransactionIntentionAsync(body, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                return new() { ClientToken = result.Data.ClientToken };
            }
            catch (ApiException ex)
            {
                logger.LogError(ex, "Error in GetNewDetails");

                return null;
            }
        }
    }
}
