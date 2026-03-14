using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Helpers.Models;
using IT.WebServices.Authorization.Payment.Paypal.Clients.Models;
using IT.WebServices.Authorization.Payment.Paypal.Helpers;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Paypal;
using IT.WebServices.Fragments.Settings;
using IT.WebServices.Helpers;
using IT.WebServices.Settings;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace IT.WebServices.Authorization.Payment.Paypal.Clients
{
    public class PaypalClient
    {
        private readonly SettingsHelper settings;
        private readonly ILogger log;

        private readonly Dictionary<uint, PlanRecordModel> CachedPlans = new();

        private Task? loginTask;
        private string? bearerToken;
        private DateTime bearerExpiration = DateTime.MinValue;
        private DateTime bearerSoftExpiration = DateTime.MinValue;

        private object syncObject = new();

        public PaypalClient(SettingsHelper settings, ILogger<PaypalClient> log)
        {
            this.settings = settings;
            this.log = log;
        }

        public bool IsEnabled => (settings.Public?.Subscription?.Paypal?.Enabled ?? false)
                              && (settings.Public?.Subscription?.Paypal?.IsValid ?? false)
                              && (settings.Owner?.Subscription?.Paypal?.IsValid ?? false);

        public async Task<PaypalNewDetails?> GetNewDetails(uint amountCents)
        {
            if (!IsEnabled)
                return null;

            var plan = await GetPlan(amountCents);
            if (plan == null)
                return null;

            return new()
            {
                AccountID = settings.Public.Subscription.Paypal.ClientID,
                PlanID = plan.id,
            };
        }

        public async Task<PlanRecordModel?> GetPlan(uint amountCents)
        {
            if (CachedPlans.TryGetValue(amountCents, out var cached))
                return cached;

            var plan = await GetPlanFromPaypal(GetPlanId(amountCents));
            if (plan != null)
            {
                CachedPlans[amountCents] = plan;
                return plan;
            }

            var created = await CreatePlan(amountCents);
            if (created != null)
            {
                CachedPlans[amountCents] = created;
                return created;
            }

            return null;
        }

        private async Task<ProductRecordModel?> GetProduct(uint amountCents)
        {
            var p = await GetProductFromPaypal(amountCents);
            if (p != null)
                return p;

            return await CreateProduct(amountCents);
        }

        private async Task<PlanRecordModel?> CreatePlan(uint amountCents)
        {
            try
            {
                await GetProduct(amountCents);

                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(3000);
                var client = await GetClient();
                if (client == null)
                    return null;

                var plan = PlanRecordModel.Create(amountCents, GetProductId(amountCents));
                plan.id = GetPlanId(amountCents);
                plan.product_id = GetProductId(amountCents);
                plan.name = (amountCents / 100.0).ToString("0.00") + " Subscription";

                var httpRes = await client.PostAsJsonAsync("/v1/billing/plans", plan, timeout.Token);

                if (httpRes.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<PlanRecordModel>(await httpRes.Content.ReadAsStringAsync());
            }
            catch { }

            return null;
        }

        private async Task<ProductRecordModel?> CreateProduct(uint amountCents)
        {
            try
            {
                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(3000);
                var client = await GetClient();
                if (client == null)
                    return null;

                var product = new ProductRecordModel()
                {
                    id = GetProductId(amountCents),
                    name = (amountCents / 100.0).ToString("0.00") + " Subscription",
                };

                var httpRes = await client.PostAsJsonAsync("/v1/catalogs/products", product, timeout.Token);

                if (httpRes.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<ProductRecordModel>(await httpRes.Content.ReadAsStringAsync());
            }
            catch { }

            return null;
        }

        public async Task<bool> CancelSubscription(string subscriptionId, string reason)
        {
            try
            {
                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(3000);
                var client = await GetClient();
                if (client == null)
                    return false;

                var httpRes = await client.PostAsJsonAsync("/v1/billing/subscriptions/" + subscriptionId + "/cancel", new { reason = reason }, timeout.Token);

                if (httpRes.IsSuccessStatusCode)
                    return true;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in CancelSubscription for {subscriptionId}", subscriptionId);
            }

            return false;
        }

        internal async Task<List<T>> GetAllPages<T>(string url) where T : BasePaginated
        {
            List<T> list = new List<T>();

            var client = await GetClient();
            if (client == null) return list;

            while (true)
            {
                //CancellationTokenSource timeout = new CancellationTokenSource();
                //timeout.CancelAfter(60000);

                var httpRes = await client.GetAsync(url/*, timeout.Token*/);

                var str = await httpRes.Content.ReadAsStringAsync();
                if (!httpRes.IsSuccessStatusCode)
                    break;

                var res = JsonSerializer.Deserialize<T>(str);
                if (res == null)
                    break;

                list.Add(res);

                var next = res?.links?.FirstOrDefault(l => l.rel == "next");
                if (next?.href == null)
                    break;

                url = next.href;
                await Task.Delay(100);
            }

            return list;
        }

        internal async Task<SubscriptionModel?> GetSubscription(string subscriptionId)
        {
            try
            {
                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(3000);
                var client = await GetClient();
                if (client == null)
                    return null;

                var httpRes = await client.GetAsync("/v1/billing/subscriptions/" + subscriptionId, timeout.Token);

                if (httpRes.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<SubscriptionModel>(await httpRes.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetSubscription");
            }

            return null;
        }

        internal async Task<List<ProcessorPaymentRecord>> GetTransactionsByDate(DateTimeOffset from, DateTimeOffset to)
        {
            try
            {
                var query = "start_date=" + from.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                query += "&end_date=" + to.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                query += "&page_size=500";
                query += "&transaction_type=T0002";

                var res = await GetAllPages<TransactionsHistoryModel>("/v1/reporting/transactions?" + query);

                return res?.SelectMany(l => l.transaction_details)
                               ?.Where(t => t?.transaction_info != null)
                               ?.Select(t => t.transaction_info!)
                               ?.Where(t => t?.paypal_reference_id_type == "SUB" || t?.paypal_reference_id_type == "RP")
                               ?.Select(t => t.ToProcessorPaymentRecord())
                               ?.ToList() ?? new();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetTransactionsByDate");
            }

            return new();
        }

        internal async IAsyncEnumerable<ProcessorPaymentRecord> GetTransactionsByDateSegmented(DateTimeOffsetRange range, CancellationToken? token = null)
        {
            var monthFrom = range.Begin;

            while (monthFrom < range.End)
            {
                token?.ThrowIfCancellationRequested();

                var monthTo = monthFrom.AddDays(1);
                if (monthTo > range.End)
                    monthTo = range.End;

                var list = await GetTransactionsByDate(monthFrom, monthTo);
                foreach (var t in list)
                    yield return t;

                monthFrom = monthTo;
            }
        }

        internal async Task<TransactionsModel> GetTransactionsForSubscription(string subscriptionId)
        {
            try
            {
                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(30000);
                var client = await GetClient();
                if (client == null)
                    return new();

                string time = "?start_time=2018-01-01T00:00:00.000Z&end_time=" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                var httpRes = await client.GetAsync("/v1/billing/subscriptions/" + subscriptionId + "/transactions" + time, timeout.Token);

                var str = await httpRes.Content.ReadAsStringAsync();
                if (httpRes.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<TransactionsModel>(str) ?? new();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetTransactionsForSubscription for {subscriptionId}", subscriptionId);
            }

            return new();
        }

        private async Task<PlanRecordModel?> GetPlanFromPaypal(string planId)
        {
            try
            {
                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(3000);
                var client = await GetClient();
                if (client == null)
                    return null;

                var httpRes = await client.GetAsync("/v1/billing/plans/" + planId, timeout.Token);

                if (httpRes.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<PlanRecordModel>(await httpRes.Content.ReadAsStringAsync());
            }
            catch { }

            return null;
        }

        private async Task<ProductRecordModel?> GetProductFromPaypal(uint amountCents)
        {
            try
            {
                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(3000);
                var client = await GetClient();
                if (client == null)
                    return null;

                var httpRes = await client.GetAsync("/v1/catalogs/products/" + GetProductId(amountCents), timeout.Token);

                if (httpRes.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<ProductRecordModel>(await httpRes.Content.ReadAsStringAsync());
            }
            catch { }

            return null;
        }

        private string GetPlanId(uint amountCents)
        {
            return "ONF-PLAN-" + amountCents;
        }

        private string GetProductId(uint amountCents)
        {
            return "ONF-PROD-" + amountCents;
        }

        private async Task<HttpClient?> GetClient()
        {
            var token = await GetBearerToken();
            if (token == null)
                return null;

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(settings.Public.Subscription.Paypal.Url);
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            client.DefaultRequestHeaders.AcceptLanguage.Add(StringWithQualityHeaderValue.Parse("en_US"));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        private async Task<string?> GetBearerToken()
        {
            var now = DateTime.UtcNow;

            if (now > bearerSoftExpiration)
            {
                lock (syncObject)
                {
                    if (loginTask == null)
                    {
                        loginTask = DoLogin();
                    }
                }
            }

            if (now > bearerExpiration && loginTask != null)
                await loginTask;

            return bearerToken;
        }

        private async Task DoLogin()
        {
            try
            {
                var pub = settings.Public.Subscription.Paypal;
                var own = settings.Owner.Subscription.Paypal;

                CancellationTokenSource timeout = new CancellationTokenSource();
                timeout.CancelAfter(3000);
                using HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(pub.Url);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
                client.DefaultRequestHeaders.AcceptLanguage.Add(StringWithQualityHeaderValue.Parse("en_US"));
                client.DefaultRequestHeaders.ConnectionClose = true;

                var authenticationString = pub.ClientID + ":" + own.ClientSecret;
                var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);

                var dict = new Dictionary<string, string>();
                dict["grant_type"] = "client_credentials";

                var httpRes = await client.PostAsync("/v1/oauth2/token", new FormUrlEncodedContent(dict), timeout.Token);

                if (httpRes.IsSuccessStatusCode)
                {
                    var jsonRes = JsonSerializer.Deserialize<OAuthResponseModel>(await httpRes.Content.ReadAsStringAsync());
                    if (jsonRes != null)
                    {
                        bearerToken = jsonRes.access_token;
                        bearerExpiration = DateTime.UtcNow.AddSeconds(jsonRes.expires_in);
                        bearerSoftExpiration = DateTime.UtcNow.AddSeconds(jsonRes.expires_in / 2);
                    }
                }
            }
            catch
            {

            }
            loginTask = null;
        }
    }
}
