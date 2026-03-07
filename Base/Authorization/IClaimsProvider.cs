using IT.WebServices.Fragments.Authorization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization
{
    public interface IClaimsProvider
    {
        Task<ClaimRecord[]> GetOtherClaims(Guid userId);
    }
}
