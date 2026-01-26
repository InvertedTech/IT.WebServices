using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IT.WebServices.Authentication;
using IT.WebServices.Content.Stats.Services.Data;
using IT.WebServices.Fragments.Content.Stats;
using IT.WebServices.Fragments.Content;
using System;
using System.Threading.Tasks;

namespace IT.WebServices.Content.Stats.Services
{
    [Authorize()]
    public class ProgressService : StatsProgressInterface.StatsProgressInterfaceBase
    {
        private readonly ILogger logger;
        private readonly IProgressDataProvider dataProvider;

        public ProgressService(ILogger<ProgressService> logger, IProgressDataProvider dataProvider)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
        }

        public override async Task<LogProgressContentResponse> LogProgressContent(LogProgressContentRequest request, ServerCallContext context)
        {
            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

            if (!Guid.TryParse(request.ContentID, out var contentId))
                return new() { Error = ContentErrorExtensions.CreateValidationError("ContentID not valid Guid") };

            if (float.IsNaN(request.Progress))
                return new() { Error = ContentErrorExtensions.CreateValidationError("Progress must be between 0 and 1") };

            if (request.Progress < 0 || request.Progress > 1)
                return new() { Error = ContentErrorExtensions.CreateValidationError("Progress must be between 0 and 1") };

            await dataProvider.LogProgress(userToken?.Id ?? Guid.Empty, contentId, request.Progress);

            return new();
        }
    }
}
