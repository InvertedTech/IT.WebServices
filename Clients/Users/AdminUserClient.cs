using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Authentication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Users
{
    public class AdminUserClient
    {
        private readonly ILogger log;
        private readonly ONUserHelper userHelper;
        private readonly UserInterface.UserInterfaceClient userClient;

        private Lazy<Task<List<UserSearchRecord>>> lazyAuthors;
        public Task<List<UserSearchRecord>> Authors => lazyAuthors.Value;

        public AdminUserClient(UserInterface.UserInterfaceClient userClient, ONUserHelper userHelper, ILogger<AdminUserClient> log)
        {
            this.userClient = userClient;
            this.userHelper = userHelper;
            this.log = log;
            lazyAuthors = new Lazy<Task<List<UserSearchRecord>>>(FetchAuthors);
        }

        public void InvalidateCache()
        {
            lazyAuthors = new Lazy<Task<List<UserSearchRecord>>>(FetchAuthors);
        }

        private async Task<List<UserSearchRecord>> FetchAuthors()
        {
            var req = new SearchUsersAdminRequest
            {
                PageSize = 500,
                PageOffset = 0,
            };
            req.Roles.AddRange(RoleAbilities.ROLE_CAN_CREATE_CONTENT.Split(','));

            var res = await userClient.SearchUsersAdminAsync(req, userHelper.GetGrpcCallOptions());
            return res.Records.ToList();
        }
    }
}
