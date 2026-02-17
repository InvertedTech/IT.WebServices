using System;
using System.Collections.Generic;
using System.Text;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Authorization.Payment.Stripe
{
    public sealed partial class StripeOwnerSettings : pb::IMessage<StripeOwnerSettings>
    {
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(ClientSecret))
                return false;

            return true;
        }
    }
}
