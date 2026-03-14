using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Combined.Helpers;
using IT.WebServices.Authorization.Payment.Generic;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ManualD = IT.WebServices.Authorization.Payment.Manual.Data;

namespace IT.WebServices.Authorization.Payment.Combined.Services
{
    [Authorize]
    public class PaymentService : PaymentInterface.PaymentInterfaceBase
    {
        private readonly ILogger logger;
        private readonly Fortis.Clients.FortisClient fortisClient;
        private readonly Paypal.Clients.PaypalClient paypalClient;
        private readonly Stripe.Clients.StripeClient stripeClient;
        private readonly ManualD.ISubscriptionRecordProvider manualProvider;
        private readonly IGenericOneTimePaymentRecordProvider genericOneTimeProvider;
        private readonly IGenericSubscriptionRecordProvider genericSubProvider;
        private readonly IGenericSubscriptionFullRecordProvider genericFullProvider;
        private readonly GenericPaymentProcessorProvider genericProcessorProvider;
        private readonly ReconcileHelper reconcileHelper;

        public PaymentService(
            ILogger<PaymentService> logger,
            Fortis.Clients.FortisClient fortisClient,
            Paypal.Clients.PaypalClient paypalClient,
            Stripe.Clients.StripeClient stripeClient,
            IGenericOneTimePaymentRecordProvider genericOneTimeProvider,
            IGenericSubscriptionRecordProvider genericSubProvider,
            IGenericSubscriptionFullRecordProvider genericFullProvider,
            ManualD.ISubscriptionRecordProvider manualProvider,
            GenericPaymentProcessorProvider genericProcessorProvider,
            ReconcileHelper reconcileHelper
        )
        {
            this.logger = logger;
            this.fortisClient = fortisClient;
            this.paypalClient = paypalClient;
            this.stripeClient = stripeClient;
            this.genericOneTimeProvider = genericOneTimeProvider;
            this.genericSubProvider = genericSubProvider;
            this.genericFullProvider = genericFullProvider;
            this.manualProvider = manualProvider;
            this.genericProcessorProvider = genericProcessorProvider;
            this.reconcileHelper = reconcileHelper;
        }

        public override async Task<CancelSubscriptionResponse> CancelOwnSubscription(CancelOwnSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                var intSubId = request.InternalSubscriptionID.ToGuid();
                if (intSubId == Guid.Empty)
                    return new() { Error = "No InternalSubscriptionID specified" };

                var record = await genericSubProvider.GetById(userToken.Id, intSubId);
                if (record == null)
                    return new() { Error = "Record not found" };

                var provider = genericProcessorProvider.GetProcessor(record);
                return await provider.CancelSubscription(record, userToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new() { Error = "Unknown error" };
            }
        }

        public override async Task<GetNewDetailsResponse> GetNewDetails(GetNewDetailsRequest request, ServerCallContext context)
        {
            try
            {
                if (request == null)
                    return new();
                if (string.IsNullOrWhiteSpace(request.SuccessUrl))
                    return new();
                if (string.IsNullOrWhiteSpace(request.CancelUrl))
                    return new();

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var level = request?.Level ?? 0;
                if (level == 0)
                    return new();

                return new()
                {
                    Fortis = await fortisClient.GetNewDetails(level, request!.PostalCode, userToken, request!.SuccessUrl, request!.CancelUrl),
                    Stripe = await stripeClient.GetNewDetails(level, userToken, request!.SuccessUrl, request!.CancelUrl),
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new();
            }
        }

        public override async Task<GetNewOneTimeDetailsResponse> GetNewOneTimeDetails(GetNewOneTimeDetailsRequest request, ServerCallContext context)
        {
            try
            {
                if (request == null)
                    return new();
                if (string.IsNullOrWhiteSpace(request?.SuccessUrl))
                    return new();
                if (string.IsNullOrWhiteSpace(request?.CancelUrl))
                    return new();

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                if (string.IsNullOrEmpty(request.InternalID))
                {
                    return new();
                }

                var details = await stripeClient.GetNewOneTimeDetails(request.InternalID, userToken, request.SuccessUrl, request.CancelUrl, request.DifferentPresetPriceCents);

                return new() { Stripe = details };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new();
            }
        }

        public override async Task<GetOneTimeRecordResponse> GetOwnOneTimeRecord(GetOwnOneTimeRecordRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var intPayId = request.InternalPaymentID.ToGuid();
                if (intPayId == Guid.Empty)
                    return new();

                var record = await genericOneTimeProvider.GetById(userToken.Id, intPayId);

                var res = new GetOneTimeRecordResponse();
                res.Generic = record;

                return res;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new();
            }
        }

        public override async Task<GetOneTimeRecordsResponse> GetOwnOneTimeRecords(GetOwnOneTimeRecordsRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var records = await genericOneTimeProvider.GetAllByUserId(userToken.Id).ToList();

                var res = new GetOneTimeRecordsResponse();
                res.Generic.AddRange(records);

                return res;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new();
            }
        }

        public override async Task<GetSubscriptionRecordResponse> GetOwnSubscriptionRecord(GetOwnSubscriptionRecordRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var intSubId = request.InternalSubscriptionID.ToGuid();
                if (intSubId == Guid.Empty)
                    return new();

                var baseT = genericFullProvider.GetBySubscriptionId(userToken.Id, intSubId);
                var manualT = manualProvider.GetBySubscriptionId(userToken.Id, intSubId);

                await Task.WhenAll(baseT, manualT);

                var res = new GetSubscriptionRecordResponse();

                if (baseT.Result != null)
                    res.Generic = baseT.Result;

                if (manualT.Result != null)
                    res.Manual = manualT.Result;

                return res;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new();
            }
        }

        public override async Task<GetSubscriptionRecordsResponse> GetOwnSubscriptionRecords(GetOwnSubscriptionRecordsRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var baseT = genericFullProvider.GetAllByUserId(userToken.Id).ToList();
                var manualT = manualProvider.GetAllByUserId(userToken.Id).ToList();

                await Task.WhenAll(baseT, manualT);

                var res = new GetSubscriptionRecordsResponse();

                if (manualT.Result != null)
                    res.Manual.AddRange(manualT.Result);

                if (baseT.Result != null)
                    res.Generic.AddRange(baseT.Result);

                return res;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new();
            }
        }

        public override async Task<ReconcileSubscriptionResponse> ReconcileOwnSubscription(ReconcileOwnSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                var intSubId = request.InternalSubscriptionID.ToGuid();
                if (intSubId == Guid.Empty)
                    return new() { Error = "No InternalSubscriptionID specified" };

                var record = await genericFullProvider.GetBySubscriptionId(userToken.Id, intSubId);
                if (record == null)
                    return new() { Error = "Record not found" };

                var provider = genericProcessorProvider.GetProcessor(record);
                return await reconcileHelper.ReconcileSubscription(record, userToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown Error");
                return new() { Error = "Unknown error" };
            }
        }
    }
}
