using IT.WebServices.Fragments.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Settings
{
    public class PublicSettingsClient
    {
        private readonly SettingsInterface.SettingsInterfaceClient client;

        private Lazy<Task<SettingsPublicData>> lazyPublicData;
        public Task<SettingsPublicData> PublicData => lazyPublicData.Value;

        public PublicSettingsClient(SettingsInterface.SettingsInterfaceClient client)
        {
            this.client = client;

            lazyPublicData = new Lazy<Task<SettingsPublicData>>(FetchData);
        }

        public void InvalidateCache()
        {
            lazyPublicData = new Lazy<Task<SettingsPublicData>>(FetchData);
        }

        private async Task<SettingsPublicData> FetchData()
        {
            var res = await client.GetPublicDataAsync(new());

            return res.Public;
        }
    }
}
