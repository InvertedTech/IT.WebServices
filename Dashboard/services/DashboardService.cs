using Grpc.Core;
using IT.WebServices.Fragments.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace IT.WebServices.Dashboard.Services
{
    [Authorize]
    public class DashboardService : DashboardInterface.DashboardInterfaceBase
    {
        private readonly ILogger<DashboardService> logger;
        private readonly IKpiMockDataProvider kpiMockDataProvider;

        public DashboardService(
            ILogger<DashboardService> logger,
            IKpiMockDataProvider kpiMockDataProvider)
        {
            this.logger = logger;
            this.kpiMockDataProvider = kpiMockDataProvider;
        }

        public override Task<GetKpisResponse> GetKpis(GetKpisRequest request, ServerCallContext context)
        {
            logger.LogInformation("Serving dashboard KPIs with mock data.");
            return Task.FromResult(kpiMockDataProvider.CreateResponse());
        }
    }
}
