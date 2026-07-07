using IT.WebServices.Fragments.Notification;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Notification
{
    public interface INotificationServiceInternal
    {
        Task<SendEmailResponse> SendEmail(SendEmailRequest request);
    }
}
