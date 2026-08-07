using IT.WebServices.Authorization.Events.Eventbrite.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Authorization.Events.Eventbrite
{
    public class EventbriteClient
    {
        private readonly ILogger log;
        private readonly HttpClient httpClient;

        public EventbriteClient(ILogger<EventbriteClient> log, HttpClient httpClient)
        {
            this.log = log;
            this.httpClient = httpClient;
        }

        public Task<EventbriteEvent?> GetEventByIdAsync(string eventId, CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }

        public Task<EventbriteEventList> ListEvents(string organizationId, CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }
    }
}
