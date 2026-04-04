using System;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Clients
{
    public class ClientGrpcHelper
    {
        public const string COMBINED_URL = "";
        public const string COMBINED_URL_DEFAULT = "http://localhost:7001";

        private readonly ILogger logger;

        public readonly GrpcChannel CombinedServiceChannel;

        public ClientGrpcHelper(IConfiguration configuration, ILogger<ClientGrpcHelper> logger)
        {
            this.logger = logger;

            var options = new GrpcChannelOptions
            {
                MaxReceiveMessageSize = null,
                MaxSendMessageSize = null,
            };

            CombinedServiceChannel = GrpcChannel.ForAddress(new Uri(GetCombinedUrl()), options);
        }

        private static string GetCombinedUrl()
        {
            var str = Environment.GetEnvironmentVariable(COMBINED_URL, EnvironmentVariableTarget.Process);
            return str ?? COMBINED_URL_DEFAULT;
        }
    }
}
