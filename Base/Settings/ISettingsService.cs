using IT.WebServices.Fragments.Settings;
using System.Threading.Tasks;

namespace IT.WebServices.Settings
{
    public interface ISettingsService
    {
        Task<GetPublicDataResponse> GetPublicDataInternal();
        Task<GetAdminDataResponse> GetAdminDataInternal();
        Task<GetOwnerDataResponse> GetOwnerDataInternal();
    }
}
