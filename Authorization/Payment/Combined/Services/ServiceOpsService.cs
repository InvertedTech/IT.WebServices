using Grpc.Core;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using IT.WebServices.Settings;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using static IT.WebServices.Fragments.Generic.ServiceStatusResponse.Types;

namespace IT.WebServices.Authorization.Payment.Combined.Services
{
    public class ServiceOpsService : ServiceOpsInterface.ServiceOpsInterfaceBase
    {
        private readonly SettingsHelper settingsClient;
        private readonly ILogger logger;

        public ServiceOpsService(ILogger<ServiceOpsService> logger, SettingsHelper settingsClient)
        {
            this.settingsClient = settingsClient;
            this.logger = logger;
        }

        public override Task<ServiceStatusResponse> ServiceStatus(ServiceStatusRequest request, ServerCallContext context)
        {
            return Task.FromResult(new ServiceStatusResponse() { Status = ServiceStatus(settingsClient) });
        }

        public static OnlineStatus ServiceStatus(SettingsHelper settingsClient)
        {
            if (!settingsClient.Public.Subscription.Paypal.Enabled)
                return OnlineStatus.Offline;

            if (!settingsClient.Public.Subscription.Paypal.IsValid)
                return OnlineStatus.Faulted;

            if (!settingsClient.Owner.Subscription.Paypal.IsValid)
                return OnlineStatus.Faulted;

            return OnlineStatus.Online;
        }
    }
}
