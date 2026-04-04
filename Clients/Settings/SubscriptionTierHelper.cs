using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Authorization;
using IT.WebServices.Fragments.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Settings
{
    public class SubscriptionTierHelper
    {
        private readonly PublicSettingsClient settingsClient;

        public SubscriptionTierHelper(PublicSettingsClient settingsClient)
        {
            this.settingsClient = settingsClient;
        }

        public bool AllowOther()
        {
            return settingsClient.PublicData.Result.Subscription?.AllowOther ?? false;
        }

        public SubscriptionTier[] GetAll()
        {
            return settingsClient.PublicData.Result.Subscription?.Tiers?.OrderBy(t => t.AmountCents)?.ToArray() ?? [];
        }

        public SubscriptionTier? GetForAmount(uint amountCents, bool strict = false)
        {
            if (amountCents < 1)
                return null;

            var tiers = GetAll();

            var tier = tiers.FirstOrDefault(c => c.AmountCents == amountCents);
            if (tier != null)
                return tier;

            if (strict)
                return null;

            return new()
            {
                AmountCents = amountCents,
                Color = "#000000",
                Name = "Other",
                Description = "Other",
            };
        }

        public SubscriptionTier? GetForUser(ONUser user)
        {
            if (user == null || user.SubscriptionLevel < 1)
                return null;

            return GetForAmount(user.SubscriptionLevel);
        }
    }
}
