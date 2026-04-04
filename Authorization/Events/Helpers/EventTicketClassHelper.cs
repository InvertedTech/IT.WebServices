using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Helpers
{
    public class EventTicketClassHelper
    {
        private readonly SettingsHelper _settingsClient;

        public EventTicketClassHelper(SettingsHelper settingsClient)
        {
            _settingsClient = settingsClient;
        }

        public TicketClassRecord[] GetAll()
        {
            return _settingsClient.Public?.Events?.TicketClasses?.ToArray() ?? [];
        }

        public TicketClassRecord? GetById(string id)
        {
            return _settingsClient.Public?.Events?.TicketClasses?.FirstOrDefault(tc =>
                tc.TicketClassId == id
            );
        }
    }
}
