using Google.Protobuf;
using IT.WebServices.Authentication.Services.Data;
using IT.WebServices.Fragments.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services
{
    public class UserServiceInternal : IUserService
    {
        private readonly IUserDataProvider dataProvider;
        private readonly IProfilePicDataProvider picProvider;

        public UserServiceInternal(IUserDataProvider dataProvider, IProfilePicDataProvider picProvider)
        {
            this.dataProvider = dataProvider;
            this.picProvider = picProvider;
        }

        public async Task AddInProfilePic(UserRecord record)
        {
            var pic = await picProvider.GetById(record.UserIDGuid);
            if (pic != null)
                record.Normal.Public.Data.ProfileImagePNG = ByteString.CopyFrom(pic);
        }

        public async Task<GetUserIdListResponse> GetUserIdListInternal()
        {
            var ret = new GetUserIdListResponse();
            try
            {
                await foreach (var r in dataProvider.GetAll())
                    ret.Records.Add(new UserIdRecord()
                    {
                        UserID = r.Normal.Public.UserID,
                        DisplayName = r.Normal.Public.Data.DisplayName,
                        UserName = r.Normal.Public.Data.UserName,
                    });
            }
            catch
            {
            }

            return ret;
        }

        public async Task<GetOtherPublicUserResponse> GetOtherPublicUserInternal(Guid userId)
        {
            var record = await dataProvider.GetById(userId);
            if (record is null)
                return new();

            await AddInProfilePic(record);

            return new() { Record = record?.Normal.Public };
        }

        public async Task<GetOtherPublicUserResponse> GetUserByOldUserID(string oldUserId)
        {
            var record = await dataProvider.GetByOldUserID(oldUserId);
            return new() { Record = record?.Normal?.Public };
        }
    }
}
