﻿using EventStore.Client;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using IT.WebServices.Content.Stats.Services.Helper;
using IT.WebServices.Content.Stats.Services.Models;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Content.Stats;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Fragments.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Content.Stats.Services.Data
{
    public class EventDdSaveDataProvider : ISaveDataProvider
    {
        private readonly EventDBHelper db;
        private const string streamBase = "stats-save-";

        public EventDdSaveDataProvider(EventDBHelper db)
        {
            this.db = db;
        }

        public async Task Save(Guid userId, Guid contentId)
        {
            var streamName = GetStreamName(userId, contentId);

            var res = await db.ConcurrentAppend(streamName, async () =>
            {
                var status = await GetCurrentStatus(streamName);

                if (status.isSaved)
                    return (null, null);

                return (status.lastRev, new SaveContentEvent()
                {
                    UserID = userId.ToString(),
                    ContentID = contentId.ToString(),
                });
            });
        }

        public async Task Unsave(Guid userId, Guid contentId)
        {
            var streamName = GetStreamName(userId, contentId);

            var res = await db.ConcurrentAppend(streamName, async () =>
            {
                var status = await GetCurrentStatus(streamName);

                if (!status.isSaved)
                    return (null, null);

                return (status.lastRev, new UnsaveContentEvent()
                {
                    UserID = userId.ToString(),
                    ContentID = contentId.ToString(),
                });
            });
        }

        private async Task<(bool isSaved, StreamRevision? lastRev)> GetCurrentStatus(string streamName)
        {
            var res = await db.GetLastEvent(streamName);
            if (res == null)
                return (false, null);

            var e = res.Value;
            switch (e.Event.EventType)
            {
                case string s when s.EndsWith(nameof(SaveContentEvent)):
                    return (true, e.Event.EventNumber.ToUInt64());
                default:
                    return (false, e.Event.EventNumber.ToUInt64());
            }
        }

        private string GetStreamName(Guid userId, Guid contentId) => streamBase + $"{userId.ToString().Replace("-", "")}-{contentId.ToString().Replace("-", "")}";
    }
}
