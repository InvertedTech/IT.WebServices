using IT.WebServices.Fragments.Authorization.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Settings
{
    public class EventTicketClassHelper
    {
        private readonly SettingsClient _settingsClient;

        public EventTicketClassHelper(SettingsClient settingsClient)
        {
            _settingsClient = settingsClient;
        }

        public TicketClassRecord[] GetAll()
        {
            return _settingsClient.PublicData?.Events?.TicketClasses?.ToArray();
        }

        public TicketClassRecord GetById(string id)
        {
            return _settingsClient.PublicData?.Events?.TicketClasses?.FirstOrDefault(tc =>
                tc.TicketClassId == id
            );
        }
    }
}
