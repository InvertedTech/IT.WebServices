using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Authorization.Payment.Combined.Services
{
    [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE_OR_BOT)]
    public class ClaimsService : ClaimsInterface.ClaimsInterfaceBase
    {
        private readonly ILogger<ClaimsService> logger;
        private readonly ClaimsServiceInternal claimsServiceInternal;

        public ClaimsService(ILogger<ClaimsService> logger, ClaimsServiceInternal claimsServiceInternal)
        {
            this.logger = logger;
            this.claimsServiceInternal = claimsServiceInternal;
        }

        public override async Task<GetClaimsResponse> GetClaims(GetClaimsRequest request, ServerCallContext context)
        {
            if (request.UserID == null)
                return new GetClaimsResponse();

            Guid userId;
            if (!Guid.TryParse(request.UserID, out userId))
                return new GetClaimsResponse();

            var res = new GetClaimsResponse();

            var claims = await claimsServiceInternal.GetOtherClaims(userId);

            res.Claims.AddRange(claims);

            return res;
        }
    }
}
