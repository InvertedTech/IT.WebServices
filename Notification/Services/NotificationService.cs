using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Notification;
using IT.WebServices.Notification.Services.Clients;
using IT.WebServices.Fragments;

namespace IT.WebServices.Notification.Services
{
    [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE_OR_BOT)]
    public class NotificationService : NotificationInterface.NotificationInterfaceBase
    {
        private readonly ILogger logger;
        private readonly SendgridClient sendgridClient;

        public NotificationService(ILogger<NotificationService> logger, SendgridClient sendgridClient)
        {
            this.logger = logger;
            this.sendgridClient = sendgridClient;
        }

        public override async Task<SendEmailResponse> SendEmail(SendEmailRequest request, ServerCallContext context)
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
