using FortisAPI.Standard.Controllers;
using FortisAPI.Standard.Exceptions;
using FortisAPI.Standard.Models;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Fortis.Clients;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Fortis;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Authorization.Payment.Fortis.Helpers
{
    public class FortisTokenHelper
    {
        private readonly FortisClient client;
        private readonly SettingsHelper settingsHelper;
        private readonly ILogger logger;

        public FortisTokenHelper(FortisClient client, SettingsHelper settingsHelper, ILogger<FortisTokenHelper> logger)
        {
            this.client = client;
            this.settingsHelper = settingsHelper;
            this.logger = logger;
        }

        public async Task<string?> CreateUpdateCardTicket(GenericSubscriptionRecord sub, ONUser userToken, CancellationToken cancellationToken)
        {
            if (client?.Client == null) return null;

            ElementsController elementsController = client.Client.ElementsController;
            var body = new V1ElementsTicketIntentionRequest()
            {
                ContactId = sub.ProcessorCustomerID,
                LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
            };

            try
            {
                ResponseTicketIntention result = await elementsController.TicketIntentionAsync(body, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                return result.Data.ClientToken;
            }
            catch (ApiException ex)
            {
                logger.LogError(ex, "Error in GetUpdateCardToken");

                return null;
            }
        }


        public async Task<string?> GetExistingTransactionToken(string dbSubId, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var res = await client.Client.TokensController.ListAllTokensRelatedAsync(
                    new Page()
                    {
                        Number = 1,
                        Size = 5000,
                    },
                    null,
                    new Filter10()
                    {
                        AccountVaultApiId = dbSubId,
                    },
                    null,
                    cancellationToken
                );

                cancellationToken.ThrowIfCancellationRequested();

                if (res?.List == null || res.List.Count == 0)
                    return null;

                return res.List[0].Id;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<string?> GetNewPreviousTransactionToken(string tranId, string dbSubId, ResponseContact contact, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var token = await GetExistingTransactionToken(dbSubId, cancellationToken);
                if (token != null)
                    return token;

                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var res = await client.Client.TokensController.CreateANewPreviousTransactionTokenAsync(new V1TokensPreviousTransactionRequest()
                    {
                        LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
                        PreviousTransactionId = tranId,
                        ContactId = contact.Data.Id,
                        AccountVaultApiId = dbSubId,
                    },
                            cancellationToken
                        );

                    if (res?.Data?.Id != null)
                        return res.Data.Id;
                }
                catch { }

                cancellationToken.ThrowIfCancellationRequested();

                token = await GetExistingTransactionToken(dbSubId, cancellationToken);
                if (token != null)
                    return token;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<string?> GetNewTokenFromTicket(string ticketId, string dbSubId, ResponseContact contact, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                return null;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }
    }
}
