using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Helpers
{
    public class EventVenueHelper
    {
        private readonly SettingsHelper _settingsClient;
        public EventVenueHelper(SettingsHelper settingsClient)
        {
            _settingsClient = settingsClient;
        }

        public EventVenue[] GetAll()
        {
            return _settingsClient.Private?.Events?.Venues?.ToArray();
        }

        public EventVenue GetById(string id)
        {
            return _settingsClient.Private?.Events?.Venues?.FirstOrDefault(v =>
                v.VenueId == id
            );
        }
    }
}
