using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Merch;
using IT.WebServices.Merch.Combined.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Merch.Combined.Services
{
    [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE_OR_BOT)]
    public class AdminMerchService : AdminMerchInterface.AdminMerchInterfaceBase
    {
        private readonly ILogger logger;
        private readonly BulkHelper bulkHelper;

        public AdminMerchService(ILogger<AdminMerchService> logger, BulkHelper bulkHelper)
        {
            this.logger = logger;
            this.bulkHelper = bulkHelper;
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override Task<MerchBulkActionCancelResponse> MerchBulkActionCancel(MerchBulkActionCancelRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return Task.FromResult(new MerchBulkActionCancelResponse());

                var res = new MerchBulkActionCancelResponse();
                res.RunningActions.AddRange(bulkHelper.CancelAction(request.Action, userToken));
                return Task.FromResult(res);
            }
            catch
            {
                return Task.FromResult(new MerchBulkActionCancelResponse());
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override Task<MerchBulkActionStartResponse> MerchBulkActionStart(MerchBulkActionStartRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return Task.FromResult(new MerchBulkActionStartResponse());

                var res = new MerchBulkActionStartResponse();
                res.RunningActions.AddRange(bulkHelper.StartAction(request.Action, userToken));
                return Task.FromResult(res);
            }
            catch
            {
                return Task.FromResult(new MerchBulkActionStartResponse());
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override Task<MerchBulkActionStatusResponse> MerchBulkActionStatus(MerchBulkActionStatusRequest request, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return Task.FromResult(new MerchBulkActionStatusResponse());

                var res = new MerchBulkActionStatusResponse();
                res.RunningActions.AddRange(bulkHelper.GetRunningActions());
                return Task.FromResult(res);
            }
            catch
            {
                return Task.FromResult(new MerchBulkActionStatusResponse());
            }
        }
    }
}
