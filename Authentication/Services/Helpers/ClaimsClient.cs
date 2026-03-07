using IT.WebServices.Authorization;
using IT.WebServices.Fragments.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Helpers
{
    public class ClaimsClient
    {
        public readonly IClaimsProvider[] claimsProviders;

        public ClaimsClient(IEnumerable<IClaimsProvider> claimsProviders)
        {
            this.claimsProviders = claimsProviders.ToArray();
        }

        public async Task<IEnumerable<ClaimRecord>> GetOtherClaims(Guid userId)
        {
            var tasks = claimsProviders.Select(p => p.GetOtherClaims(userId));

            await Task.WhenAll(tasks);

            Dictionary<string, ClaimRecord> dict = new Dictionary<string, ClaimRecord>();

            foreach(var t in tasks)
            {
                foreach (var claim in await t)
                {
                    if (!dict.ContainsKey(claim.Name))
                    {
                        dict[claim.Name] = claim;
                        continue;
                    }

                    if (dict[claim.Name].ExpiresOnUTC < claim.ExpiresOnUTC)
                    {
                        dict[claim.Name] = claim;
                    }
                }
            }

            return dict.Values;
        }
    }
}
