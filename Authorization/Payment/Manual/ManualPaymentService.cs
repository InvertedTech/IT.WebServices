using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Manual.Data;
using IT.WebServices.Fragments.Authorization.Payment.Manual;
using IT.WebServices.Fragments.Generic;

namespace IT.WebServices.Authorization.Payment.Manual
{
    public class ManualPaymentService : ManualPaymentInterface.ManualPaymentInterfaceBase
    {
        private readonly ILogger logger;
        private readonly ISubscriptionRecordProvider subscriptionProvider;

        public ManualPaymentService(ILogger<ManualPaymentService> logger, ISubscriptionRecordProvider subscriptionProvider)
        {
            this.logger = logger;
            this.subscriptionProvider = subscriptionProvider;
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ManualCancelOtherSubscriptionResponse> ManualCancelOtherSubscription(ManualCancelOtherSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request?.UserID == null)
                    return new() { Error = "No UserId specified" };

                var userId = request.UserID.ToGuid();
                if (userId == Guid.Empty)
                    return new() { Error = "No UserId specified" };

                if (request?.SubscriptionID == null)
                    return new() { Error = "No SubscriptionID specified" };

                var subId = request.SubscriptionID.ToGuid();
                if (subId == Guid.Empty)
                    return new() { Error = "No SubscriptionID specified" };

                var record = await subscriptionProvider.GetBySubscriptionId(userId, subId);
                if (record == null)
                    return new() { Error = "Record not found" };

                record.CanceledBy = userToken.Id.ToString();
                record.CanceledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                await subscriptionProvider.Save(record);

                return new()
                {
                    Record = record
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
                return new() { Error = "Unknown error" };
            }
        }

        public override async Task<ManualCancelOwnSubscriptionResponse> ManualCancelOwnSubscription(ManualCancelOwnSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                var subId = request.SubscriptionID.ToGuid();
                if (subId == Guid.Empty)
                    return new() { Error = "No SubscriptionID specified" };

                var record = await subscriptionProvider.GetBySubscriptionId(userToken.Id, subId);
                if (record == null)
                    return new() { Error = "Record not found" };

                record.CanceledBy = userToken.Id.ToString();
                record.CanceledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                await subscriptionProvider.Save(record);

                return new()
                {
                    Record = record
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
                return new() { Error = "Unknown error" };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ManualGetOtherSubscriptionRecordsResponse> ManualGetOtherSubscriptionRecords(ManualGetOtherSubscriptionRecordsRequest request, ServerCallContext context)
        {
            try
            {
                if (request?.UserID == null)
                    return new();

                var userId = request.UserID.ToGuid();
                if (userId == Guid.Empty)
                    return new();

                var res = subscriptionProvider.GetAllByUserId(userId);

                var ret = new ManualGetOtherSubscriptionRecordsResponse();
                await foreach (var record in res)
                    ret.Records.Add(record);

                return ret;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
            }

            return new();
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ManualGetOtherSubscriptionRecordResponse> ManualGetOtherSubscriptionRecord(ManualGetOtherSubscriptionRecordRequest request, ServerCallContext context)
        {
            try
            {
                if (request?.UserID == null)
                    return new();

                var userId = request.UserID.ToGuid();
                if (userId == Guid.Empty)
                    return new();

                var subId = request.SubscriptionID.ToGuid();
                if (subId == Guid.Empty)
                    return new();

                return new()
                {
                    Record = await subscriptionProvider.GetBySubscriptionId(userId, subId)
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
            }

            return new();
        }

        public override async Task<ManualGetOwnSubscriptionRecordsResponse> ManualGetOwnSubscriptionRecords(ManualGetOwnSubscriptionRecordsRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var res = subscriptionProvider.GetAllByUserId(userToken.Id);

                var ret = new ManualGetOwnSubscriptionRecordsResponse();
                await foreach (var record in res)
                    ret.Records.Add(record);

                return ret;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
            }

            return new();
        }

        public override async Task<ManualGetOwnSubscriptionRecordResponse> ManualGetOwnSubscriptionRecord(ManualGetOwnSubscriptionRecordRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var subId = request.SubscriptionID.ToGuid();
                if (subId == Guid.Empty)
                    return new();

                return new()
                {
                    Record = await subscriptionProvider.GetBySubscriptionId(userToken.Id, subId)
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
            }

            return new();
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ManualNewOtherSubscriptionResponse> ManualNewOtherSubscription(ManualNewOtherSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                var userId = request.UserID.ToGuid();
                if (userId == Guid.Empty)
                    return new() { Error = "No UserId specified" };

                var record = new ManualSubscriptionRecord()
                {
                    UserID = userId.ToString(),
                    SubscriptionID = Guid.NewGuid().ToString(),
                    AmountCents = request.AmountCents,
                    CreatedBy = userToken.Id.ToString(),
                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    ModifiedBy = userToken.Id.ToString(),
                    ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                };

                await subscriptionProvider.Save(record);

                return new()
                {
                    Record = record
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
                return new() { Error = "Unknown error" };
            }
        }

        public override async Task<ManualNewOwnSubscriptionResponse> ManualNewOwnSubscription(ManualNewOwnSubscriptionRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = "No user token specified" };

                if (request == null)
                    return new() { Error = "Level not valid" };

                var record = new ManualSubscriptionRecord()
                {
                    UserID = userToken.Id.ToString(),
                    SubscriptionID = Guid.NewGuid().ToString(),
                    AmountCents = request.AmountCents,
                    CreatedBy = userToken.Id.ToString(),
                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    ModifiedBy = userToken.Id.ToString(),
                    ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                };

                await subscriptionProvider.Save(record);

                return new()
                {
                    Record = record
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");
                return new() { Error = "Unknown error" };
            }
        }
    }
}
