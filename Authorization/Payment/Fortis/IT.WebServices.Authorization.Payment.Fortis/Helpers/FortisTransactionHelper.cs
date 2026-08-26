using FortisAPI.Standard.Controllers;
using FortisAPI.Standard.Exceptions;
using FortisAPI.Standard.Models;
using IT.WebServices.Authorization.Payment.Fortis.Clients;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Helpers;
using System.Runtime.CompilerServices;

namespace IT.WebServices.Authorization.Payment.Fortis.Helpers
{
    public class FortisTransactionHelper
    {
        private readonly FortisClient client;
        private readonly SettingsHelper settingsHelper;
        private readonly IGenericSubscriptionRecordProvider localSubProvider;

        public FortisTransactionHelper(FortisClient client, SettingsHelper settingsHelper, IGenericSubscriptionRecordProvider localSubProvider)
        {
            this.client = client;
            this.settingsHelper = settingsHelper;
            this.localSubProvider = localSubProvider;
        }

        public async Task<GenericPaymentRecord?> CreateFromAccountValut(string accountVaultId, int fixAmount)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                var res = await client.Client.TransactionsCreditCardController.CCSaleTokenizedAsync(new V1TransactionsCcSaleTokenRequest()
                {
                    AccountVaultId = accountVaultId,
                    TransactionAmount = fixAmount,
                    Description = "Amount Fix",
                });

                return res.ToGenericPaymentRecord();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async IAsyncEnumerable<ProcessorPaymentRecord> GetAllForRange(DateTimeOffsetRange range, [EnumeratorCancellation] CancellationToken cancellationToken, int? amount = null, string? contactId = null, int triesLeft = 5, string? state = null)
        {
            if (!client.IsEnabled) yield break;
            if (client.Client == null) yield break;

            var ranges = range.BreakIntoHours();
            foreach (var r in ranges)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.Write(r.Begin.ToString() + "-" + r.End.ToString() + ": ");

                var payments = await GetAll(r, cancellationToken, amount, contactId, triesLeft, state);

                foreach (var p in payments)
                    yield return p;
            }
        }

        private async Task<List<ProcessorPaymentRecord>> GetAll(DateTimeOffsetRange range, CancellationToken cancellationToken, int? amount = null, string? contactId = null, int triesLeft = 5, string? state = null)
        {
            if (!client.IsEnabled) return [];
            if (client.Client == null) return [];

            int errors = 0;
            int page = 1;
            int size = 100;

            var ret = new List<List11>();

            try
            {
                while (errors < 10)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var list = await client.Client.TransactionsReadController.ListTransactionsAsync(
                                new Page() { Number = page, Size = size },
                                null,
                                new Filter11()
                                {
                                    CreatedTs = new()
                                    {
                                        Lower = range.Begin.ToUnixTimeSeconds(),
                                        Upper = range.End.ToUnixTimeSeconds(),
                                    },
                                    TransactionAmount = amount,
                                    ContactId = contactId,
                                    LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
                                    ProductTransactionId = settingsHelper.Owner.Subscription.Fortis.ProductID,
                                    BillingAddress = state == null ? null : new() { State = state },
                                });

                    if (list?.List == null)
                    {
                        errors++;
                        continue;
                    }

                    if (ret.Any(i => i.Id == list.List.FirstOrDefault()?.Id))
                        break;

                    ret.AddRange(list.List);

                    page++;

                    Console.WriteLine($"Loading Transactions: {ret.Count}");

                    if (list.List.Count < size)
                        break;

                    if (ret.Count > 10000)
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);

                if (triesLeft > 0)
                    return await GetAll(range, cancellationToken, amount, contactId, triesLeft - 1, state);
                else
                    throw;
            }

            return ret.Select(p => p.ToProcessorPaymentRecord()).ToList();
        }

        public async Task<GenericPaymentRecord?> Get(string tranId, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                var res = await client.Client.TransactionsReadController.GetTransactionAsync(tranId, null, cancellationToken);

                return res.Data.ToGenericPaymentRecord();
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<List<GenericPaymentRecord>> GetByApiId(long tranId)
        {
            if (!client.IsEnabled) return [];
            if (client.Client == null) return [];

            try
            {
                var res = await client.Client.TransactionsReadController.ListTransactionsAsync(new Page() { Number = 1, Size = 1 }, null, new Filter11()
                {
                    TransactionApiId = "\"" + tranId.ToString() + "\""
                }, null);

                return res.ToGenericPaymentRecords();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return [];
        }

        public async Task<string> GetNewPaymentIntent(uint amount)
        {
            if (!client.IsEnabled) return "";
            if (client.Client == null) return "";

            ElementsController elementsController = client.Client.ElementsController;
            var body = new V1ElementsTransactionIntentionRequest()
            {
                Action = ActionEnum.Sale,
                Amount = (int)amount * 100,
                Methods = new List<Method>(),
                LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID
            };
            body.Methods.Add(new Method(TypeEnum.Cc, settingsHelper.Owner.Subscription.Fortis.ProductID));

            try
            {
                ResponseTransactionIntention result = await elementsController.TransactionIntentionAsync(body);
                return result.Data.ClientToken;
            }
            catch (ApiException e)
            {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
                return "";
            }
        }

        public async Task<GenericPaymentRecord?> ProcessOneTimeSale(string ccTokenId, uint cents)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                var res = await client.Client.TransactionsCreditCardController.CCSaleTokenizedAsync(new V1TransactionsCcSaleTokenRequest()
                {
                    TransactionApiId = ccTokenId,
                    TransactionAmount = (int)cents,
                });

                return res.ToGenericPaymentRecord();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<bool> ReRunFailedPayment(GenericPaymentRecord record, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return false;
            if (client.Client == null) return false;

            try
            {
                var res = await client.Client.DeclinedRecurring.ReRunRecurringPaymentAsync(record.ProcessorPaymentID, cancellationToken);

                return res.Data.Status == StatusId2Enum.Enum101;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return false;
        }
    }
}
