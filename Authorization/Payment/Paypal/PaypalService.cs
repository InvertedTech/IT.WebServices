using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Authorization.Payment.Paypal.Clients;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Paypal;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Authorization.Payment.Paypal
{
    public class PaypalService : PaypalInterface.PaypalInterfaceBase
    {
        private readonly ILogger<PaypalService> logger;
        private readonly IGenericSubscriptionFullRecordProvider fullProvider;
        private readonly IGenericSubscriptionRecordProvider subProvider;
        private readonly IGenericPaymentRecordProvider paymentProvider;
        private readonly PaypalClient client;

        public PaypalService(ILogger<PaypalService> logger, IGenericSubscriptionFullRecordProvider fullProvider, IGenericSubscriptionRecordProvider subProvider, IGenericPaymentRecordProvider paymentProvider, PaypalClient client)
        {
            this.logger = logger;
            this.fullProvider = fullProvider;
            this.subProvider = subProvider;
            this.paymentProvider = paymentProvider;
            this.client = client;
        }

        #region New
        public override async Task<PaypalNewOwnSubscriptionResponse> PaypalNewOwnSubscription(PaypalNewOwnSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request?.PaypalSubscriptionID == null)
                    return new() { Error = "SubscriptionId not valid" };

                var sub = await client.GetSubscription(request.PaypalSubscriptionID);
                if (sub == null)
                    return new() { Error = "SubscriptionId not valid" };

                var billing_info = sub.billing_info;
                if (billing_info == null)
                    return new() { Error = "SubscriptionId not valid" };

                decimal value = 0;
                if (!decimal.TryParse(sub.billing_info?.last_payment?.amount?.value ?? "0", out value))
                    return new() { Error = "Subscription Value not valid" };

                var record = new GenericSubscriptionRecord()
                {
                    UserID = userToken.Id.ToString(),
                    InternalSubscriptionID = Guid.NewGuid().ToString(),
                    ProcessorSubscriptionID = request.PaypalSubscriptionID,
                    AmountCents = (uint)(value * 100),
                    Status = Fragments.Authorization.Payment.SubscriptionStatus.SubscriptionActive,
                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                };

                await subProvider.Save(record);

                var payment = new GenericPaymentRecord()
                {
                    UserID = userToken.Id.ToString(),
                    InternalSubscriptionID = record.InternalSubscriptionID,
                    InternalPaymentID = Guid.NewGuid().ToString(),
                    ProcessorPaymentID = sub.id,
                    AmountCents = (uint)(value * 100),
                    Status = Fragments.Authorization.Payment.PaymentStatus.PaymentComplete,
                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    PaidOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(sub.create_time),
                    PaidThruUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(billing_info.next_billing_time),
                };

                await paymentProvider.Save(payment);

                return new()
                {
                    Record = record
                };
            }
            catch
            {
                return new() { Error = "Unknown error" };
            }
        }
        #endregion
    }
}
