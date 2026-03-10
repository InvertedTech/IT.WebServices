using FortisAPI.Standard.Controllers;
using FortisAPI.Standard.Exceptions;
using FortisAPI.Standard.Models;
using IT.WebServices.Authentication;
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

        public readonly FortisAPI.Standard.FortisAPIClient Client;

        public FortisClient(SettingsHelper settingsHelper, IOptions<AppSettings> appSettings, ILogger<FortisClient> logger)
        {
            this.settingsHelper = settingsHelper;
            this.appSettings = appSettings.Value;
            this.logger = logger;

            logger.LogWarning("FortisDeveloperId: {FortisDeveloperId}", this.appSettings.FortisDeveloperId);
            logger.LogWarning("UserID: {UserID}", settingsHelper.Owner.Subscription.Fortis.UserID);
            logger.LogWarning("UserApiKey: {UserApiKey}", settingsHelper.Owner.Subscription.Fortis.UserApiKey);
            logger.LogWarning("LocationID: {LocationID}", settingsHelper.Owner.Subscription.Fortis.LocationID);
            logger.LogWarning("ProductID: {ProductID}", settingsHelper.Owner.Subscription.Fortis.ProductID);

            Client = GetClient();
        }

        private FortisAPI.Standard.FortisAPIClient GetClient()
        {
            FortisAPI.Standard.FortisAPIClient client = new FortisAPI.Standard.FortisAPIClient.Builder()
                .CustomHeaderAuthenticationCredentials(settingsHelper.Owner.Subscription.Fortis.UserID, settingsHelper.Owner.Subscription.Fortis.UserApiKey, appSettings.FortisDeveloperId)
                .Environment(settingsHelper.Public.Subscription.Fortis.IsTest ? FortisAPI.Standard.Environment.Sandbox : FortisAPI.Standard.Environment.Production)
                .HttpClientConfig(config => config.NumberOfRetries(0))
                .Build();

            return client;
        }

        public async Task<FortisNewDetails> GetNewDetails(uint amountCents, string postalCode, ONUser userToken, string successUrl, string cancelUrl)
        {
            ElementsController elementsController = Client.ElementsController;
            var body = new V1ElementsTransactionIntentionRequest()
            {
                Action = ActionEnum.Sale,
                Amount = (int)amountCents,
                TaxAmount = null,
                Methods = new(),
                LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID
            };
            body.Methods.Add(new(TypeEnum.Cc, settingsHelper.Owner.Subscription.Fortis.ProductID));

            try
            {
                ResponseTransactionIntention result = await elementsController.TransactionIntentionAsync(body);
                return new() { ClientToken = result.Data.ClientToken };
            }
            catch (ApiException ex)
            {
                logger.LogError(ex, "Error in GetNewDetails");

                return new();
            }
        }
    }
}
