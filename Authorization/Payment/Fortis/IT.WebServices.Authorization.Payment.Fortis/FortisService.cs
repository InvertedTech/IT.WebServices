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
        private readonly FortisSubscriptionHelper fortisSubscriptionHelper;
        private readonly FortisTransactionHelper fortisTransactionHelper;

        public FortisService(ILogger<FortisService> logger, IGenericSubscriptionFullRecordProvider fullProvider, IGenericPaymentRecordProvider paymentProvider, IGenericSubscriptionRecordProvider subscriptionProvider, FortisSubscriptionHelper fortisSubscriptionHelper, FortisTransactionHelper fortisTransactionHelper)
        {
            this.logger = logger;
            this.fullProvider = fullProvider;
            this.paymentProvider = paymentProvider;
            this.subscriptionProvider = subscriptionProvider;
            this.fortisSubscriptionHelper = fortisSubscriptionHelper;
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
    }
}
