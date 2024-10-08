﻿using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Authentication
{
    public sealed partial class UserRecord : pb::IMessage<UserRecord>
    {
        public Guid UserIDGuid
        {
            get => Normal.Public.UserIDGuid;
            set => Normal.Public.UserIDGuid = value;
        }
    }

    public sealed partial class UserNormalRecord : pb::IMessage<UserNormalRecord>
    {
        public Guid UserIDGuid
        {
            get => Public.UserIDGuid;
            set => Public.UserIDGuid = value;
        }

        public UserSearchRecord ToUserSearchRecord()
        {
            var record =  new UserSearchRecord()
            {
                UserID = Public.UserID,
                UserName = Public.Data.UserName,
                DisplayName = Public.Data.DisplayName,
                Email = Private.Data.Email,
                CreatedOnUTC = Public.CreatedOnUTC,
                ModifiedOnUTC = Public.ModifiedOnUTC,
                DisabledOnUTC = Public.DisabledOnUTC,
            };

            record.Roles.AddRange(record.Roles);

            return record;
        }
    }

    public sealed partial class UserPublicRecord : pb::IMessage<UserPublicRecord>
    {
        public Guid UserIDGuid
        {
            get => UserID.ToGuid();
            set => UserID = value.ToString();
        }
    }

    public sealed partial class TOTPDevice : pb::IMessage<TOTPDevice>
    {
        public bool IsValid => IsVerified && !IsDisabled;
        public bool IsVerified => VerifiedOnUTC != null;
        public bool IsDisabled => DisabledOnUTC != null;

        public TOTPDeviceLimited ToLimited()
        {
            return new ()
            {
                TotpID = TotpID,
                DeviceName = DeviceName,
                CreatedOnUTC = CreatedOnUTC,
            };
        }
    }
}
