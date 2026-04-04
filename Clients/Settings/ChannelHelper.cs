using IT.WebServices.Fragments.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Settings
{
    public class ChannelHelper
    {
        private readonly PublicSettingsClient settingsClient;

        public ChannelHelper(PublicSettingsClient settingsClient)
        {
            this.settingsClient = settingsClient;
        }

        public ChannelRecord[] GetAll()
        {
            return settingsClient.PublicData.Result.CMS?.Channels?.ToArray() ?? []
            ;
        }

        public ChannelRecord? GetChannelById(string id)
        {
            return settingsClient.PublicData.Result.CMS?.Channels?.FirstOrDefault(c => c.ChannelId == id);
        }

        public ChannelRecord? GetChannelBySlug(string slug)
        {
            return settingsClient.PublicData.Result.CMS?.Channels?.FirstOrDefault(c => c.UrlStub == slug);
        }
    }
}
