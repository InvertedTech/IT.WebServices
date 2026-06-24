using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Stripe.Clients;
using IT.WebServices.Authorization.Payment.Stripe.Data;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Stripe;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using IT.WebServices.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Authorization.Payment.Stripe
{
    public class StripeService : StripeInterface.StripeInterfaceBase
    {
        private readonly ILogger<StripeService> logger;
        private readonly IGenericSubscriptionFullRecordProvider fullProvider;
        private readonly IGenericSubscriptionRecordProvider subscriptionProvider;
        private readonly IGenericPaymentRecordProvider paymentProvider;
        private readonly StripeClient client;

        public StripeService(
            ILogger<StripeService> logger,
            IGenericSubscriptionFullRecordProvider fullProvider,
            IGenericSubscriptionRecordProvider subscriptionProvider,
            IGenericPaymentRecordProvider paymentProvider,
            StripeClient client
        )
        {
            this.logger = logger;
            this.fullProvider = fullProvider;
            this.subscriptionProvider = subscriptionProvider;
            this.paymentProvider = paymentProvider;
            this.client = client;
        }

        //[Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        //public override async Task<StripeCheckOtherSubscriptionResponse> StripeCheckOtherSubscription(
        //    StripeCheckOtherSubscriptionRequest request,
        //    ServerCallContext context
        //)
        //{
        //    try
        //    {
        //        var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
        //        if (userToken == null)
        //            return new() { Error = "No user token specified" };

        //        var userId = request.UserID.ToGuid();

        //        var customer = await client.GetCustomerByUserId(userId);
        //        if (customer == null)
        //            return new() { };

        //        var stripeSubs = await client.GetSubscriptionsByCustomerId(customer.Id);

        //        var dbSubs = await subscriptionProvider.GetAllByUserId(userId).ToList();

        //        foreach (var stripeSub in stripeSubs)
        //        {
        //            var dbSub = dbSubs.FirstOrDefault(s => s.ProcessorSubscriptionID == stripeSub.ProcessorSubscriptionID);
        //            if (dbSub == null)
        //            {
        //                dbSub = stripeSub;
        //                dbSub.UserID = userId.ToString();
        //                dbSub.InternalSubscriptionID = Guid.NewGuid().ToString();
        //                dbSub.CreatedBy = userToken.Id.ToString();
        //                dbSub.ModifiedBy = userToken.Id.ToString();
        //                dbSub.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

        //                await subscriptionProvider.Save(dbSub);

        //                var dbPayment = new GenericPaymentRecord()
        //                {
        //                    UserID = userId.ToString(),
        //                    InternalSubscriptionID = dbSub.InternalSubscriptionID,
        //                    InternalPaymentID = Guid.NewGuid().ToString(),
        //                    ProcessorPaymentID = stripeSub.LatestInvoiceId,
        //                    AmountCents = dbSub.AmountCents,
        //                    Status =
        //                        dbSub.Status == SubscriptionStatus.SubscriptionActive
        //                            ? PaymentStatus.PaymentComplete
        //                            : PaymentStatus.PaymentFailed,
        //                    CreatedOnUTC = dbSub.CreatedOnUTC,
        //                    ModifiedOnUTC = dbSub.ModifiedOnUTC,
        //                    PaidOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(stripeSub.Items.FirstOrDefault()?.CurrentPeriodStart ?? DateTime.MinValue),
        //                    PaidThruUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(stripeSub.Items.FirstOrDefault()?.CurrentPeriodEnd.AddDays(5) ?? DateTime.MinValue),
        //                };

        //                await paymentProvider.Save(dbPayment);
        //            }
        //        }

        //        var ret = new StripeCheckOtherSubscriptionResponse();
        //        ret.Records.AddRange(await fullProvider.GetAllByUserId(userId).ToList());

        //        return ret;
        //    }
        //    catch
        //    {
        //        return new() { Error = "Unknown error" };
        //    }
        //}

        //public override async Task<StripeCheckOwnSubscriptionResponse> StripeCheckOwnSubscription(StripeCheckOwnSubscriptionRequest request, ServerCallContext context)
        //{
        //    try
        //    {
        //        var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
        //        if (userToken == null)
        //            return new() { Error = "No user token specified" };

        //        var customer = await client.GetCustomerByUserId(userToken.Id);
        //        if (customer == null)
        //            return new() { };

        //        var stripeSubs = await client.GetSubscriptionsByCustomerId(customer.Id);

        //        var dbSubs = await subscriptionProvider.GetAllByUserId(userToken.Id).ToList();

        //        foreach (var stripeSub in stripeSubs)
        //        {
        //            var dbSub = dbSubs.FirstOrDefault(s => s.ProcessorSubscriptionID == stripeSub.Id);
        //            if (dbSub == null)
        //            {
        //                dbSub = new()
        //                {
        //                    UserID = userToken.Id.ToString(),
        //                    InternalSubscriptionID = Guid.NewGuid().ToString(),
        //                    ProcessorSubscriptionID = stripeSub.Id.ToString(),
        //                    ProcessorCustomerID = customer.Id,
        //                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(stripeSub.Created),
        //                    ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
        //                    Status = ConvertStatus(stripeSub.Status),
        //                    AmountCents = (uint)(stripeSub.Items.FirstOrDefault()?.Plan?.Amount ?? 0),
        //                };

        //                await subscriptionProvider.Save(dbSub);

        //                var dbPayment = new GenericPaymentRecord()
        //                {
        //                    UserID = userToken.Id.ToString(),
        //                    InternalSubscriptionID = dbSub.InternalSubscriptionID,
        //                    InternalPaymentID = Guid.NewGuid().ToString(),
        //                    ProcessorPaymentID = stripeSub.LatestInvoiceId,
        //                    AmountCents = dbSub.AmountCents,
        //                    Status =
        //                        dbSub.Status == SubscriptionStatus.SubscriptionActive
        //                            ? PaymentStatus.PaymentComplete
        //                            : PaymentStatus.PaymentFailed,
        //                    CreatedOnUTC = dbSub.CreatedOnUTC,
        //                    ModifiedOnUTC = dbSub.ModifiedOnUTC,
        //                    PaidOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(stripeSub.Items.FirstOrDefault()?.CurrentPeriodStart ?? DateTime.MinValue),
        //                    PaidThruUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(stripeSub.Items.FirstOrDefault()?.CurrentPeriodEnd.AddDays(5) ?? DateTime.MinValue),
        //                };

        //                await paymentProvider.Save(dbPayment);
        //            }
        //        }

        //        //dbSubs = await subscriptionProvider.GetAllByUserId(userToken.Id);
        //        //foreach (var dbSub in dbSubs)
        //        //    await EnsureAllPayments(dbSub);

        //        var ret = new StripeCheckOwnSubscriptionResponse();
        //        ret.Records.AddRange(await fullProvider.GetAllByUserId(userToken.Id).ToList());

        //        return ret;
        //    }
        //    catch
        //    {
        //        return new() { Error = "Unknown error" };
        //    }
        //}

        //public override async Task<StripeCheckOwnOneTimeResponse> StripeCheckOwnOneTime(StripeCheckOwnOneTimeRequest request, ServerCallContext context)
        //{
        //    try
        //    {
        //        var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
        //        if (userToken == null)
        //            return new() { Error = "No user token specified" };

        //        var customer = await client.GetCustomerByUserId(userToken.Id);
        //        if (customer == null)
        //            return new() { };

        //        var payments = await client.GetOneTimePaymentsByCustomerId(customer.Id);

        //        var dbPayments = await oneTimeProvider.GetAllByUserId(userToken.Id);

        //        foreach (var stripePayment in payments)
        //        {
        //            var dbPayment = dbPayments.FirstOrDefault(s => s.StripePaymentID == stripePayment.Id);
        //            if (dbPayment == null)
        //            {
        //                var checkout = await client.GetCheckoutSessionByPaymentIntentId(stripePayment.Id);
        //                if (checkout == null)
        //                    continue;

        //                var lineItem = checkout.LineItems.FirstOrDefault();
        //                if (lineItem == null)
        //                    continue;

        //                dbPayment = new()
        //                {
        //                    UserID = userToken.Id.ToString(),
        //                    PaymentID = Guid.NewGuid().ToString(),
        //                    StripePaymentID = stripePayment.Id.ToString(),
        //                    InternalID = lineItem.Price.ProductId.Replace(StripeClient.PRODUCT_ONETIME_PREFIX, ""),
        //                    Status = ConvertPaymentStatus(stripePayment.Status),
        //                    AmountCents = (uint)stripePayment.Amount,
        //                    TaxCents = 0,
        //                    TaxRateThousandPercents = 0,
        //                    TotalCents = (uint)stripePayment.Amount,
        //                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(stripePayment.Created),
        //                    CreatedBy = userToken.Id.ToString(),
        //                    ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
        //                    ModifiedBy = userToken.Id.ToString(),
        //                    PaidOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(stripePayment.Created),
        //                };

        //                await oneTimeProvider.Save(dbPayment);
        //            }
        //            else
        //            {
        //                var newStatus = ConvertPaymentStatus(stripePayment.Status);
        //                if (dbPayment.Status != newStatus)
        //                {
        //                    dbPayment.Status = newStatus;
        //                    dbPayment.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
        //                    dbPayment.ModifiedBy = userToken.Id.ToString();

        //                    await oneTimeProvider.Save(dbPayment);
        //                }
        //            }
        //        }

        //        var ret = new StripeCheckOwnOneTimeResponse();
        //        ret.Records.AddRange(await oneTimeProvider.GetAllByUserId(userToken.Id));

        //        return ret;
        //    }
        //    catch
        //    {
        //        return new() { Error = "Unknown error" };
        //    }
        //}

        //private async Task EnsureAllPayments(ONUser userToken, StripeSubscriptionRecord dbSub)
        //{
        //    var dbPayments = paymentProvider.GetAllBySubscriptionId(userToken.Id, dbSub.SubscriptionID.ToGuid());
        //    var stripePayments = client.GetPaymentsBySubscriptionId(dbSub.StripeSubscriptionID);
        //}

        //[Authorize(Roles = ONUser.ROLE_CAN_CREATE_CONTENT)]
        //public override async Task<StripeEnsureOneTimeProductResponse> StripeEnsureOneTimeProduct(StripeEnsureOneTimeProductRequest request, ServerCallContext context)
        //{
        //    try
        //    {
        //        var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
        //        if (userToken == null)
        //            return new() { Error = "No user token specified" };

        //        var product = await client.EnsureOneTimeProduct(request);
        //        if (product == null)
        //            return new() { Error = "Failed To Get A Response From Stripe Client" };

        //        var price = await client.EnsureOneTimePrice(request, product);
        //        if (price == null)
        //            return new() { Error = "Failed To Get A Response From Stripe Client" };

        //        await client.EnsureOneTimeProductDefaultPrice(product, price);

        //        return new();
        //    }
        //    catch (Exception e)
        //    {
        //        return new() { Error = e.Message };
        //    }
        //}

        //public override async Task<StripeGetOwnSubscriptionRecordsResponse> StripeGetOwnSubscriptionRecords(StripeGetOwnSubscriptionRecordsRequest request, ServerCallContext context)
        //{
        //    var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
        //    if (userToken == null)
        //        return new ();

        //    var res = new StripeGetOwnSubscriptionRecordsResponse();
        //    res.Records.AddRange(await dataMerger.GetAllByUserId(userToken.Id));

        //    return res;
        //}

        public override async Task<StripeFinishOwnSubscriptionResponse> StripeFinishOwnSubscription(StripeFinishOwnSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var utcNow = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request == null)
                    return new() { Error = "Request not valid" };

                if (request.ProcessorSessionID == null)
                    return new() { Error = "ProcessorSessionID not valid" };

                var session = await client.GetCheckoutSessionById(request.ProcessorSessionID, context.CancellationToken);
                if (session == null)
                    return new() { Error = "ProcessorSessionID not valid" };

                context.CancellationToken.ThrowIfCancellationRequested();

                var newSubRecord = await client.GetSubscription(session.SubscriptionId, context.CancellationToken);
                if (newSubRecord == null)
                    return new() { Error = "SessionId not valid" };

                var newSubId = Guid.NewGuid().ToString();

                var curSubRecord = await subscriptionProvider.GetByProcessorId(newSubRecord.ProcessorSubscriptionID);
                if (curSubRecord != null)
                    newSubId = curSubRecord.InternalSubscriptionID;

                decimal value = newSubRecord.AmountCents;
                if (value <= 0)
                    return new() { Error = "Subscription amount not valid" };

                newSubRecord.UserID = userToken.Id.ToString();
                newSubRecord.InternalSubscriptionID = newSubId;
                newSubRecord.CreatedBy = userToken.Id.ToString();
                newSubRecord.ModifiedBy = userToken.Id.ToString();

                var fullRecord = new GenericSubscriptionFullRecord()
                {
                    SubscriptionRecord = newSubRecord,
                };

                context.CancellationToken.ThrowIfCancellationRequested();

                var payments = await client.GetAllPaymentsForSubscription(newSubRecord.ProcessorSubscriptionID, context.CancellationToken);

                context.CancellationToken.ThrowIfCancellationRequested();

                var newPaymentRecord = payments.FirstOrDefault();

                if (newPaymentRecord != null)
                {
                    newPaymentRecord.UserID = userToken.Id.ToString();
                    newPaymentRecord.InternalSubscriptionID = newSubRecord.InternalSubscriptionID;
                    newPaymentRecord.InternalPaymentID = Guid.NewGuid().ToString();
                    newPaymentRecord.CreatedBy = userToken.Id.ToString();
                    newPaymentRecord.ModifiedBy = userToken.Id.ToString();

                    fullRecord.Payments.Add(newPaymentRecord);
                }

                context.CancellationToken.ThrowIfCancellationRequested();

                await fullProvider.Save(fullRecord);

                return new()
                {
                    Record = newSubRecord
                };
            }
            catch
            {
                return new() { Error = "Unknown error" };
            }
        }

        public override async Task<StripeFinishUpdateOwnCardResponse> StripeFinishUpdateOwnCard(StripeFinishUpdateOwnCardRequest request, ServerCallContext context)
        {
            try
            {
                var utcNow = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request == null)
                    return new() { Error = "Request not valid" };

                if (string.IsNullOrWhiteSpace(request.InternalSubscriptionID))
                    return new() { Error = "InternalSubscriptionID not valid" };
                if (request.ProcessorSessionID == null)
                    return new() { Error = "ProcessorSessionID not valid" };

                var session = await client.GetCheckoutSessionById(request.ProcessorSessionID, context.CancellationToken);
                if (session == null)
                    return new() { Error = "ProcessorSessionID not valid" };

                context.CancellationToken.ThrowIfCancellationRequested();

                var dbSubRecord = await subscriptionProvider.GetById(userToken.Id, request.InternalSubscriptionID.ToGuid());
                if (dbSubRecord == null)
                    return new() { Error = "Could not find subscription" };

                var providerSubRecord = await client.GetSubscription(dbSubRecord.ProcessorSubscriptionID, context.CancellationToken);
                if (providerSubRecord == null)
                    return new() { Error = "SessionId not valid" };

                context.CancellationToken.ThrowIfCancellationRequested();

                await client.UpdateSubscriptionWithTokenizedCard(userToken, dbSubRecord, request.ProcessorSessionID, context.CancellationToken);

                context.CancellationToken.ThrowIfCancellationRequested();

                dbSubRecord.ModifiedBy = userToken.Id.ToString();

                await subscriptionProvider.Save(dbSubRecord);

                return new()
                {
                    Record = providerSubRecord
                };
            }
            catch
            {
                return new() { Error = "Unknown error" };
            }
        }

        public override async Task<StripeStartUpdateOwnCardResponse> StripeStartUpdateOwnCard(StripeStartUpdateOwnCardRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request == null)
                    return new() { Error = "Request not valid" };

                if (string.IsNullOrWhiteSpace(request.InternalSubscriptionID))
                    return new() { Error = "InternalSubscriptionID not valid" };
                if (string.IsNullOrWhiteSpace(request.SuccessUrl))
                    return new() { Error = "SuccessUrl not valid" };
                if (string.IsNullOrWhiteSpace(request.CancelUrl))
                    return new() { Error = "CancelUrl not valid" };

                var sub = await subscriptionProvider.GetById(userToken.Id, request.InternalSubscriptionID.ToGuid());
                if (sub == null)
                    return new() { Error = "Could not find subscription" };

                context.CancellationToken.ThrowIfCancellationRequested();

                var paymentLink = await client.CreateTokenizeNewCardSession(userToken, sub, request.SuccessUrl, request.CancelUrl, context.CancellationToken);

                context.CancellationToken.ThrowIfCancellationRequested();

                return new()
                {
                    PaymentLink = paymentLink,
                };
            }
            catch
            {
                return new() { Error = "Unknown error" };
            }
        }
    }
}
