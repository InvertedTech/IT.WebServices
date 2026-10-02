using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Fortis.Clients;
using IT.WebServices.Authorization.Payment.Fortis.Helpers;
using IT.WebServices.Authorization.Payment.Fortis.Models;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Fortis;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using IT.WebServices.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI;

namespace IT.WebServices.Authorization.Payment.Fortis
{
    public class FortisService : FortisInterface.FortisInterfaceBase
    {
        private readonly ILogger<FortisService> logger;
        private readonly IGenericSubscriptionFullRecordProvider fullProvider;
        private readonly IGenericPaymentRecordProvider paymentProvider;
        private readonly IGenericSubscriptionRecordProvider subscriptionProvider;
        private readonly FortisClient fortisClient;
        private readonly FortisSubscriptionHelper fortisSubscriptionHelper;
        private readonly FortisTokenHelper fortisTokenHelper;
        private readonly FortisTransactionHelper fortisTransactionHelper;

        public FortisService(ILogger<FortisService> logger, IGenericSubscriptionFullRecordProvider fullProvider, IGenericPaymentRecordProvider paymentProvider, IGenericSubscriptionRecordProvider subscriptionProvider, FortisClient fortisClient, FortisSubscriptionHelper fortisSubscriptionHelper, FortisTokenHelper fortisTokenHelper, FortisTransactionHelper fortisTransactionHelper)
        {
            this.logger = logger;
            this.fullProvider = fullProvider;
            this.paymentProvider = paymentProvider;
            this.subscriptionProvider = subscriptionProvider;
            this.fortisClient = fortisClient;
            this.fortisSubscriptionHelper = fortisSubscriptionHelper;
            this.fortisTokenHelper = fortisTokenHelper;
            this.fortisTransactionHelper = fortisTransactionHelper;
        }

        public override async Task<FortisFinishOwnSubscriptionResponse> FortisFinishOwnSubscription(FortisFinishOwnSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var utcNow = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request == null)
                    return new() { Error = "Request not valid" };

                if (string.IsNullOrWhiteSpace(request.TransactionID))
                    return new() { Error = "TransactionID not valid" };

                var transaction = await fortisTransactionHelper.Get(request.TransactionID, context.CancellationToken);
                if (transaction == null)
                    return new() { Error = "TransactionID not valid" };
                if (transaction.Status != PaymentStatus.PaymentComplete)
                    return new() { Error = "Transaction status not valid" };

                var curPayRecord = await paymentProvider.GetByProcessorId(request.TransactionID);
                if (curPayRecord != null)
                    return new()
                    {
                        Record = await subscriptionProvider.GetById(userToken.Id, curPayRecord.InternalSubscriptionID.ToGuid())
                    };

                var newSubRecord = await fortisSubscriptionHelper.CreateFromTransaction(request.TransactionID, UserModel.FromUserToken(userToken), 1, context.CancellationToken);
                if (newSubRecord == null)
                    return new() { Error = "Session not created" };

                newSubRecord.UserID = userToken.Id.ToString();
                newSubRecord.CreatedOnUTC = utcNow;
                newSubRecord.ModifiedOnUTC = utcNow;
                newSubRecord.CreatedBy = userToken.Id.ToString();
                newSubRecord.ModifiedBy = userToken.Id.ToString();

                //var newPaymentRecord = new GenericPaymentRecord()
                //{
                //    UserID = userToken.Id.ToString(),
                //    InternalSubscriptionID = newSubRecord.InternalSubscriptionID,
                //    InternalPaymentID = Guid.NewGuid().ToString(),
                //    ProcessorPaymentID = sub.LatestInvoiceId,
                //    Status = PaymentStatus.PaymentComplete,
                //    AmountCents = (uint)(value),
                //    TaxCents = 0,
                //    TaxRateThousandPercents = 0,
                //    TotalCents = (uint)(value),
                //    CreatedOnUTC = utcNow,
                //    ModifiedOnUTC = utcNow,
                //    PaidOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(sub.Created),
                //    PaidThruUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(sub.Created.AddDays(1)),
                //    CreatedBy = userToken.Id.ToString(),
                //    ModifiedBy = userToken.Id.ToString(),
                //};

                transaction.UserID = userToken.Id.ToString();
                transaction.InternalSubscriptionID = newSubRecord.InternalSubscriptionID;
                transaction.InternalPaymentID = Guid.NewGuid().ToString();
                transaction.CreatedOnUTC = utcNow;
                transaction.ModifiedOnUTC = utcNow;
                transaction.CreatedBy = userToken.Id.ToString();
                transaction.ModifiedBy = userToken.Id.ToString();

                var fullRecord = new GenericSubscriptionFullRecord()
                {
                    SubscriptionRecord = newSubRecord,
                };
                fullRecord.Payments.Add(transaction);

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

        public override async Task<FortisFinishUpdateOwnCardResponse> FortisFinishUpdateOwnCard(FortisFinishUpdateOwnCardRequest request, ServerCallContext context)
        {
            try
            {
                var utcNow = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request == null)
                    return new() { Error = "Request not valid" };

                if (string.IsNullOrWhiteSpace(request.TransactionID))
                    return new() { Error = "TransactionID not valid" };

                var transaction = await fortisTransactionHelper.Get(request.TransactionID, context.CancellationToken);
                if (transaction == null)
                    return new() { Error = "TransactionID not valid" };
                if (transaction.Status != PaymentStatus.PaymentComplete)
                    return new() { Error = "Transaction status not valid" };

                if (string.IsNullOrWhiteSpace(request.InternalSubscriptionID))
                    return new() { Error = "InternalSubscriptionID not valid" };

                var fullRecord = await fullProvider.GetBySubscriptionId(userToken.Id, request.InternalSubscriptionID.ToGuid());
                if (fullRecord == null)
                    return new() { Error = "Could not find subscription" };

                var sub = fullRecord.SubscriptionRecord;

                var curPayRecord = await paymentProvider.GetByProcessorId(request.TransactionID);
                if (curPayRecord != null)
                    return new()
                    {
                        Record = sub,
                    };

                var success = await fortisSubscriptionHelper.UpdateCardFromTransaction(sub, request.TransactionID, UserModel.FromUserToken(userToken), context.CancellationToken);
                if (!success)
                    return new() { Error = "Error updating subscription" };

                sub.ModifiedOnUTC = utcNow;
                sub.ModifiedBy = userToken.Id.ToString();

                transaction.UserID = userToken.Id.ToString();
                transaction.InternalSubscriptionID = sub.InternalSubscriptionID;
                transaction.InternalPaymentID = Guid.NewGuid().ToString();
                transaction.CreatedOnUTC = utcNow;
                transaction.ModifiedOnUTC = utcNow;
                transaction.CreatedBy = userToken.Id.ToString();
                transaction.ModifiedBy = userToken.Id.ToString();

                fullRecord.Payments.Add(transaction);

                await fullProvider.Save(fullRecord);

                return new()
                {
                    Record = sub,
                };
            }
            catch
            {
                return new() { Error = "Unknown error" };
            }
        }

        public override async Task<FortisStartUpdateOwnCardResponse> FortisStartUpdateOwnCard(FortisStartUpdateOwnCardRequest request, ServerCallContext context)
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

                var sub = await subscriptionProvider.GetById(userToken.Id, request.InternalSubscriptionID.ToGuid());
                if (sub == null)
                    return new() { Error = "Could not find subscription" };

                context.CancellationToken.ThrowIfCancellationRequested();

                var newDetails = await fortisClient.GetNewDetails(sub.AmountCents, sub.TaxCents, userToken, context.CancellationToken);
                if (newDetails == null)
                    return new() { Error = "Error creating token" };

                context.CancellationToken.ThrowIfCancellationRequested();

                return new() { ClientToken = newDetails.ClientToken };
            }
            catch
            {
                return new() { Error = "Unknown error" };
            }
        }

        //public override async Task<FortisStartUpdateOwnCardResponse> FortisStartUpdateOwnCard(FortisStartUpdateOwnCardRequest request, ServerCallContext context)
        //{
        //    try
        //    {
        //        var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
        //        if (userToken == null)
        //            return new() { Error = "No user token specified" };

        //        if (request == null)
        //            return new() { Error = "Request not valid" };

        //        if (string.IsNullOrWhiteSpace(request.InternalSubscriptionID))
        //            return new() { Error = "InternalSubscriptionID not valid" };

        //        var sub = await subscriptionProvider.GetById(userToken.Id, request.InternalSubscriptionID.ToGuid());
        //        if (sub == null)
        //            return new() { Error = "Could not find subscription" };

        //        context.CancellationToken.ThrowIfCancellationRequested();

        //        var clientToken = await fortisTokenHelper.CreateUpdateCardTicket(sub, userToken, context.CancellationToken);

        //        context.CancellationToken.ThrowIfCancellationRequested();

        //        return new() { ClientToken = clientToken };
        //    }
        //    catch
        //    {
        //        return new() { Error = "Unknown error" };
        //    }
        //}
    }
}
