using Grpc.Core;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Notification;
using IT.WebServices.Notification.Services.Clients;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Notification.Services.Services
{
    public class NotificationServiceInternal
    {
        private readonly ILogger log;
        private readonly SendgridClient sendgridClient;

        public NotificationServiceInternal(ILogger<NotificationServiceInternal> log, SendgridClient sendgridClient)
        {
            this.log = log;
            this.sendgridClient = sendgridClient;
        }

        public async Task<SendEmailResponse> SendEmail(SendEmailRequest request)
        {
            try
            {
                var error = await sendgridClient.SendEmail(request);
                if (error != null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonDeliveryFailed, error) };

                return new();
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown Error") };
            }
        }
    }
}
