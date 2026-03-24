using System;
using System.Collections.Generic;
using System.Text;
using static Google.Api.FieldInfo.Types;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Authorization.Discord
{
    public sealed partial class DiscordMemberRecord : pb::IMessage<DiscordMemberRecord>
    {
        public Guid UserIdGuid
        {
            get => UserIdGuid;
            set => UserIdGuid = value;
        }
    }
}
