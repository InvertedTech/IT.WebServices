﻿using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IT.WebServices.Authentication;
using IT.WebServices.Content.Stats.Services.Data;
using IT.WebServices.Fragments.Content.Stats;
using System;
using System.Threading.Tasks;

namespace IT.WebServices.Content.Stats.Services
{
    [Authorize()]
    public class SaveService : StatsSaveInterface.StatsSaveInterfaceBase
    {
        private readonly ILogger logger;
        private readonly ISaveDataProvider dataProvider;

        public SaveService(ILogger<SaveService> logger, ISaveDataProvider dataProvider)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
        }

        public override async Task<SaveContentResponse> SaveContent(SaveContentRequest request, ServerCallContext context)
        {
            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken == null || !userToken.IsLoggedIn)
                return new();

            if (!Guid.TryParse(request.ContentID, out var contentId))
                return new();

            await dataProvider.Save(userToken.Id, contentId);

            return new();
        }

        public override async Task<SaveContentResponse> UnsaveContent(SaveContentRequest request, ServerCallContext context)
        {
            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken == null || !userToken.IsLoggedIn)
                return new();

            if (!Guid.TryParse(request.ContentID, out var contentId))
                return new();

            await dataProvider.Unsave(userToken.Id, contentId);

            return new();
        }
    }
}
