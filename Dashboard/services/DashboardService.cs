using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Dashboard.Services.Data;
using IT.WebServices.Fragments.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace IT.WebServices.Dashboard.Services
{
    [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE_OR_BOT)]
    public class DashboardService : DashboardInterface.DashboardInterfaceBase
    {
        private readonly ILogger<DashboardService> logger;
        private readonly IUserDataProvider users;
        private readonly ISubscriptionDataProvider subs;

        public DashboardService(
            ILogger<DashboardService> logger,
            IUserDataProvider users,
            ISubscriptionDataProvider subs)
        {
            this.logger = logger;
            this.users = users;
            this.subs = subs;
        }

        public override async Task<GetKpisResponse> GetKpis(GetKpisRequest request, ServerCallContext context)
        {
            var usersTask = GetUserKpis();
            var subscriptionsTask = GetSubscriptionKpis();

            await Task.WhenAll(usersTask, subscriptionsTask);

            return new GetKpisResponse
            {
                Users = await usersTask,
                Subscriptions = await subscriptionsTask,
            };
        }

        private  async Task<UserKpis> GetUserKpis()
        {
            return await users.GetUserKpis();
        }

        private async Task<SubscriptionKpis> GetSubscriptionKpis()
        {
            return await subs.GetSubscriptionKpis();
        }
    }
}
