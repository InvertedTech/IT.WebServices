using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Notification;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Notifications
{
    public class NotificationClient
    {
        private readonly NotificationInterface.NotificationInterfaceClient client;
        private readonly ONUserHelper userHelper;
        private readonly ILogger log;

        public NotificationClient(NotificationInterface.NotificationInterfaceClient client, ONUserHelper userHelper, ILogger<NotificationClient> log)
        {
            this.client = client;
            this.userHelper = userHelper;
            this.log = log;
        }

        public async Task<SendEmailResponse> SendEmail(SendEmailRequest request, CancellationToken cancellationToken = default)
        {
            return await client.SendEmailAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));
        }
    }
}
