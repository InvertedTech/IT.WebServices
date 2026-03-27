using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Content;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Content.CMS
{
    public interface IAssetService
    {
        Task<CreateAssetResponse> CreateAssetInternal(CreateAssetRequest request, ONUser user);
    }
}
