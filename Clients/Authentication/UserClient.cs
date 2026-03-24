using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Fragments.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<UserSearchRecord> GetUserByEmailAsync(string email)
        {
            var client = new UserInterface.UserInterfaceClient(_helper.UserServiceChannel);
            var res = await client.SearchUsersAdminAsync(
                new SearchUsersAdminRequest { SearchString = email, PageSize = 1 },
                GetMetadata()
            );
            return res?.Records?.FirstOrDefault();
        }

        public async Task LinkDiscordAsync(string userId, string discordId, string accessToken, string refreshToken, DateTime accessTokenExpiresOnUtc)
        {
            var client = new UserInterface.UserInterfaceClient(_helper.UserServiceChannel);
            await client.ModifyOtherUserAuthProvidersAsync(
                new ModifyOtherUserAuthProvidersRequest
                {
                    UserID = userId,
                    AuthProviders = new AuthProviders
                    {
                        Discord = new DiscordAuthProvider
                        {
                            DiscordId = discordId,
                            AccessToken = accessToken,
                            RefreshToken = refreshToken,
                            AccessTokenExpiresOnUTC = Timestamp.FromDateTime(accessTokenExpiresOnUtc)
                        }
                    }
                },
                GetMetadata()
            );
        }

        public async Task UnlinkDiscordAsync(string userId)
        {
            var client = new UserInterface.UserInterfaceClient(_helper.UserServiceChannel);
            await client.ModifyOtherUserAuthProvidersAsync(
                new ModifyOtherUserAuthProvidersRequest
                {
                    UserID = userId,
                    AuthProviders = new AuthProviders()
                },
                GetMetadata()
            );
        }

        private Metadata GetMetadata()
        {
            var data = new Metadata();
            data.Add("Authorization", "Bearer " + _helper.ServiceToken.Value);
            return data;
        }
    }
}
