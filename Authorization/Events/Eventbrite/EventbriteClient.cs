using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Events.Eventbrite
{
    public class EventbriteClient
    {
        private readonly ILogger log; 

        public EventbriteClient(ILogger<EventbriteClient> log)
        {
            this.log = log;
        }
    }
}
