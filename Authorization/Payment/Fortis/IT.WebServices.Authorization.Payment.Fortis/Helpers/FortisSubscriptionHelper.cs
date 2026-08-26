using FortisAPI.Standard.Models;
using IT.WebServices.Authorization.Payment.Fortis.Clients;
using IT.WebServices.Authorization.Payment.Fortis.Models;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Helpers;
using System;

namespace IT.WebServices.Authorization.Payment.Fortis.Helpers
{
    public class FortisSubscriptionHelper
    {
        private readonly FortisClient client;
        private readonly FortisContactHelper contactHelper;
        private readonly FortisTokenHelper tokenHelper;
        private readonly FortisTransactionHelper tranHelper;
        private readonly SettingsHelper settingsHelper;

        public FortisSubscriptionHelper(FortisClient client, FortisContactHelper contactHelper, FortisTokenHelper tokenHelper, FortisTransactionHelper tranHelper, SettingsHelper settingsHelper)
        {
            this.client = client;
            this.contactHelper = contactHelper;
            this.tokenHelper = tokenHelper;
            this.tranHelper = tranHelper;
            this.settingsHelper = settingsHelper;
        }

        public async Task<GenericSubscriptionRecord?> Create(string tokenId, int amountCents, DateTime startDate, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var res = await client.Client.RecurringController.CreateANewRecurringRecordAsync(
                        new V1RecurringsRequest()
                        {
                            Active = ActiveEnum.Enum1,
                            AccountVaultId = tokenId,
                            Interval = 1,
                            IntervalType = IntervalTypeEnum.M,
                            LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
                            StartDate = startDate.ToString("yyyy-MM-dd"),
                            TransactionAmount = amountCents,
                            PaymentMethod = PaymentMethodEnum.Cc,
                        },
                        cancellationToken
                    );

                cancellationToken.ThrowIfCancellationRequested();

                if (res is null)
                    return null;

                var res2 = await Get(res.Data.Id, cancellationToken);

                return res2;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<GenericSubscriptionRecord?> CreateFromTransaction(string tranId, UserModel user, uint monthsForFirst, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var trans = await tranHelper.Get(tranId, cancellationToken);
                if (trans == null)
                {
                    Console.WriteLine($"Error in CreateSubscriptionFromTransaction tranId={tranId}. GetTransaction returned null.");
                    return null;
                }

                if (user == null)
                {
                    Console.WriteLine($"Error in CreateSubscriptionFromTransaction tranId={tranId}. User is null.");
                    return null;
                }

                try
                {
                    var startDate = trans.PaidOnUTC.ToDateTime();
                    var recStartDate = startDate.AddMonths((int)monthsForFirst);

                    while (recStartDate.AddDays(-1) < DateTime.UtcNow)
                        recStartDate = recStartDate.AddDays(1);

                    cancellationToken.ThrowIfCancellationRequested();

                    var contact = await contactHelper.Create(user, cancellationToken);
                    if (contact == null)
                        return null;

                    var dbSubId = Guid.NewGuid();

                    var token = await tokenHelper.GetNewPreviousTransactionToken(tranId, "s" + dbSubId.ToString().Replace("-", ""), contact, cancellationToken);
                    if (token == null)
                    {
                        Console.WriteLine($"Error in CreateSubscriptionFromTransaction tranId={tranId}. Token is null. Failed to create a token.");
                        return null;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    var dbSub = await Create(token, (int)trans.TotalCents, recStartDate, cancellationToken);
                    dbSub?.InternalSubscriptionID = dbSubId.ToString();

                    cancellationToken.ThrowIfCancellationRequested();

                    return dbSub;
                }
                catch (Exception ex)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
                }
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task Cancel(string subscriptionId, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return;
            if (client.Client == null) return;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var res = await client.Client.RecurringController.DeleteRecurringRecordAsync(subscriptionId, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }
        }

        public async Task<GenericSubscriptionRecord?> ChangeAmount(GenericSubscriptionRecord sub, int newAmount, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var res = await client.Client.RecurringController.UpdateRecurringPaymentAsync(sub.ProcessorSubscriptionID, new V1RecurringsRequest1() { TransactionAmount = newAmount }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (res is null)
                    return null;

                var res2 = await Get(res.Data.Id, cancellationToken);

                return res2;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<GenericSubscriptionRecord?> Get(string subscriptionId, CancellationToken cancellationToken, bool? active = null, int triesLeft = 5)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var list = await client.Client.RecurringController.ListAllRecurringRecordAsync(
                        new Page() { Number = 1, Size = 1 },
                        null,
                        new Filter6()
                        {
                            Active = active is null ? null : (active == true ? ActiveEnum.Enum1 : ActiveEnum.Enum0),
                            LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
                            ProductTransactionId = settingsHelper.Owner.Subscription.Fortis.ProductID,
                            Id = subscriptionId,
                        },
                        new() { "account_vault" },
                        cancellationToken
                    );

                var sub = list?.List?.FirstOrDefault();

                cancellationToken.ThrowIfCancellationRequested();

                return sub?.ToSubscriptionRecord();
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (triesLeft > 0)
                    return await Get(subscriptionId, cancellationToken, active, triesLeft - 1);
                else
                    Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<GenericSubscriptionFullRecord?> GetWithTransactions(string subscriptionId, CancellationToken cancellationToken, int triesLeft = 10)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var expand = new List<string>();
                expand.Add("transactions");
                expand.Add("account_vault");

                var list = await client.Client.RecurringController.ListAllRecurringRecordAsync(
                        new Page() { Number = 1, Size = 1 },
                        null,
                        new Filter6()
                        {
                            LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
                            ProductTransactionId = settingsHelper.Owner.Subscription.Fortis.ProductID,
                            Id = subscriptionId,
                        },
                        expand,
                        cancellationToken
                    );

                var sub = list?.List?.FirstOrDefault();

                cancellationToken.ThrowIfCancellationRequested();

                return sub?.ToSubscriptionFullRecord();
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (triesLeft > 0)
                    return await GetWithTransactions(subscriptionId, cancellationToken, triesLeft - 1);
                else
                    Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<List<GenericSubscriptionRecord>> GetAll(CancellationToken cancellationToken, bool? active = null, int? amount = null, int triesLeft = 100)
        {
            if (!client.IsEnabled) return [];
            if (client.Client == null) return [];

            int errors = 0;
            int page = 1;
            int size = 1000;

            var ret = new List<List6>();

            try
            {
                while (errors < 100)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var list = await client.Client.RecurringController.ListAllRecurringRecordAsync(
                            new Page() { Number = page, Size = size },
                            null,
                            new Filter6()
                            {
                                Active = active is null ? null : (active == true ? ActiveEnum.Enum1 : ActiveEnum.Enum0),
                                TransactionAmount = amount,
                                LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
                                ProductTransactionId = settingsHelper.Owner.Subscription.Fortis.ProductID,
                            },
                            new() { "account_vault" },
                            cancellationToken
                        );

                    if (list?.List == null)
                    {
                        errors++;
                        continue;
                    }

                    if (ret.Any(i => i.Id == list.List.FirstOrDefault()?.Id))
                        break;

                    ret.AddRange(list.List);

                    page++;

                    Console.WriteLine($"Loading Subscriptions: {ret.Count}");

                    cancellationToken.ThrowIfCancellationRequested();

                    if (list.List.Count < size)
                        break;
                }
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);

                if (triesLeft > 0)
                    return await GetAll(cancellationToken, active, amount, triesLeft - 1);
            }

            if (ret.Count % size == 0 && ret.Count != 0)
                throw new Exception($"{ret.Count} is divisible by {size} this normally indicates an error. Aborting!");

            return ret.Select(r => r.ToSubscriptionRecord()).ToList();
        }

        public async Task<List<GenericSubscriptionRecord>> GetByContactId(string contactId, CancellationToken cancellationToken)
        {
            if (!client.IsEnabled) return [];
            if (client.Client == null) return [];

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var res = await client.Client.RecurringController.ListAllRecurringRecordAsync(
                        new Page() { Number = 1, Size = 5000 },
                        null,
                        new Filter6()
                        {
                            AccountVaultId = contactId
                        },
                        new() { "account_vault" },
                        cancellationToken
                    );

                cancellationToken.ThrowIfCancellationRequested();

                return res.ToSubscriptionRecords();
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return [];
        }
    }
}
