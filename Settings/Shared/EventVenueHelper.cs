using IT.WebServices.Fragments.Authorization.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Settings
{
    public class EventVenueHelper
    {
        private readonly SettingsClient _settingsClient;
        public EventVenueHelper(SettingsClient settingsClient)
        {
            _settingsClient = settingsClient;
        }

        public EventVenue[] GetAll()
        {
            return _settingsClient.PrivateData?.Events?.Venues?.ToArray();
        }

        public EventVenue GetById(string id)
        {
            return _settingsClient.PrivateData?.Events?.Venues?.FirstOrDefault(v =>
                v.VenueId == id
            );
        }
    }
}
