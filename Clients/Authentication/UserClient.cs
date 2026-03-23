using Grpc.Core;
using IT.WebServices.Fragments.Authentication;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Authentication
{
    public class UserClient 
    {
        public readonly ClientGrpcHelper _helper;

        public UserClient(ClientGrpcHelper helper)
        {
            _helper = helper;
        }

        public async Task<UserNormalRecord> GetOtherUserAsync(string userId)
        {
            var client = new UserInterface.UserInterfaceClient(_helper.UserServiceChannel);
            var res = await client.GetOtherUserAsync(
                    new GetOtherUserRequest
                    {
                        UserID = userId.ToString(),
                    },
                    GetMetadata()
                );

            return res?.Record;
        }

        private Metadata GetMetadata()
        {
            var data = new Metadata();
            data.Add("Authorization", "Bearer " + _helper.ServiceToken);
            return data;
        }
    }
}
