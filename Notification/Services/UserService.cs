using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IT.WebServices.Notification.Services.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Notification;

namespace IT.WebServices.Notification.Services
{
    [Authorize]
    public class UserService : UserNotificationInterface.UserNotificationInterfaceBase
    {
        private readonly ILogger logger;
        private readonly INotificationUserDataProvider notificationDataProvider;
        private readonly IUserNotificationDataProvider userDataProvider;

        public UserService(ILogger<UserService> logger, INotificationUserDataProvider notificationDataProvider, IUserNotificationDataProvider userDataProvider)
        {
            this.logger = logger;
            this.notificationDataProvider = notificationDataProvider;
            this.userDataProvider = userDataProvider;
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE_OR_BOT)]
        public override async Task<GetAllTokensResponse> GetAllTokens(GetAllTokensRequest request, ServerCallContext context)
        {
            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken == null)
                return new();

            var disabled = new List<string>();

            if (!request.IncludeDisabledPush)
            {
                await foreach (var data in userDataProvider.GetAll())
                    if (data.Normal.DisableAllPush)
                        disabled.Add(data.UserID);
            }

            var tokens = new List<string>();

            await foreach (var data in notificationDataProvider.GetAll())
                if (!disabled.Contains(data.UserID))
                    tokens.Add(data.TokenID);

            var ret = new GetAllTokensResponse();
            ret.TokenIDs.AddRange(tokens);

            return ret;
        }

        public override async Task<GetRecordResponse> GetRecord(GetRecordRequest request, ServerCallContext context)
        {
            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken == null)
                return new();

            var record = await userDataProvider.GetById(userToken.Id);

            return new() { Record = record };
        }

        public override async Task<ModifyNormalRecordResponse> ModifyNormalRecord(ModifyNormalRecordRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = NotificationErrorExtensions.CreateUnauthorizedError("modify notification record") };

                var record = await userDataProvider.GetById(userToken.Id);
                if (record == null)
                {
                    record = new()
                    {
                        UserID = userToken.Id.ToString(),
                        CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    };
                }

                record.Normal = request.Record;
                record.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                await userDataProvider.Save(record);

                return new();
            }
            catch
            {
                return new() { Error = NotificationErrorExtensions.CreateError(NotificationErrorReason.NotificationErrorUnknown, "Unknown error occurred") };
            }
        }

        public override async Task<RegisterNewTokenResponse> RegisterNewToken(RegisterNewTokenRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = NotificationErrorExtensions.CreateUnauthorizedError("register notification token") };

                if (string.IsNullOrWhiteSpace(request.TokenID))
                    return new() { Error = NotificationErrorExtensions.CreateValidationError("TokenID is required") };

                var record = await notificationDataProvider.GetByTokenId(request.TokenID);
                if (record == null)
                    record = new()
                    {
                        CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    };

                record.TokenID = request.TokenID;
                record.UserIDGuid = userToken.Id;
                record.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                await notificationDataProvider.Save(record);

                return new();
            }
            catch
            {
                return new() { Error = NotificationErrorExtensions.CreateError(NotificationErrorReason.NotificationErrorUnknown, "Unknown error occurred") };
            }
        }

        public override async Task<UnRegisterNewTokenResponse> UnRegisterNewToken(UnRegisterNewTokenRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = NotificationErrorExtensions.CreateUnauthorizedError("unregister notification token") };

                if (string.IsNullOrWhiteSpace(request.TokenID))
                    return new() { Error = NotificationErrorExtensions.CreateValidationError("TokenID is required") };

                var record = await notificationDataProvider.GetByTokenId(request.TokenID);
                if (record == null)
                    return new();

                if (record.TokenID != request.TokenID)
                    return new();

                if (record.UserIDGuid != userToken.Id)
                    return new();

                await notificationDataProvider.Delete(request.TokenID);

                return new();
            }
            catch
            {
                return new() { Error = NotificationErrorExtensions.CreateError(NotificationErrorReason.NotificationErrorUnknown, "Unknown error occurred") };
            }
        }
    }
}
