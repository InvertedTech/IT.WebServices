using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Content.Stats.Services.Data;
using IT.WebServices.Content.Stats.Services.Data.Sql;
using IT.WebServices.Fragments.Content.Stats;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Google.Rpc.Context.AttributeContext.Types;

namespace IT.WebServices.Content.Stats.Services
{
    [Authorize()]
    public class QuerySqlService : StatsQueryInterface.StatsQueryInterfaceBase
    {
        private readonly ILogger logger;
        private readonly MySQLHelper sql;
        private readonly IStatsUserPrivateDataProvider userPriv;

        public QuerySqlService(ILogger<QuerySqlService> logger, MySQLHelper sql, IStatsUserPrivateDataProvider userPriv)
        {
            this.logger = logger;
            this.sql = sql;
            this.userPriv = userPriv;
        }

        [AllowAnonymous]
        public override async Task<GetContentStatsResponse> GetContentStats(GetContentStatsRequest request, ServerCallContext context)
        {
            var response = new GetContentStatsResponse();

            if (!Guid.TryParse(request.ContentID, out var contentId))
                return response;

            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken != null && userToken.IsLoggedIn)
            {
                response = await GetContentRecordByIds(contentId, userToken.Id);
            }

            var cRecord = await GetContentCountsById(contentId);
            response.Record = cRecord;

            return response;
        }

        public override async Task<GetOwnUserSavesResponse> GetOwnUserSaves(GetOwnUserSavesRequest request, ServerCallContext context)
        {
            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken == null && !userToken.IsLoggedIn)
                return new();

            var rec = await userPriv.GetById(userToken.Id);

            var res = new GetOwnUserSavesResponse();

            res.SavedContentIDs.AddRange(rec.Saves);

            return res;
        }

        private async Task<StatsContentPublicRecord> GetContentCountsById(Guid contentId)
        {
            var record = new StatsContentPublicRecord() { ContentID = contentId.ToString() };

            const string query = @"
                SELECT
                    COALESCE(SUM(CASE WHEN LikedOnUTC IS NOT NULL THEN 1 ELSE 0 END), 0) Likes,
                    COALESCE(SUM(CASE WHEN SavedOnUTC IS NOT NULL THEN 1 ELSE 0 END), 0) Saves,
                    COALESCE(SUM(NumberOfShares), 0) Shares,
                    COALESCE(SUM(NumberOfViews), 0) Views
                FROM
                    Stats_ContentUser
                WHERE
                    ContentID = @ContentID
            ";

            var parameters = new MySqlParameter[]
            {
                new MySqlParameter("ContentID", contentId.ToString()),
            };

            using var rdr = await sql.ReturnReader(query, parameters);
            if (await rdr.ReadAsync())
            {
                record.Likes = (ulong)rdr.GetInt32(0);
                record.Saves = (ulong)rdr.GetInt32(1);
                record.Shares = (ulong)rdr.GetInt32(2);
                record.Views = (ulong)rdr.GetInt32(3);
            }

            return record;
        }

        private async Task<GetContentStatsResponse> GetContentRecordByIds(Guid contentId, Guid userId)
        {
            var record = new GetContentStatsResponse();

            const string query = @"
                SELECT
                    LikedOnUTC,
                    SavedOnUTC,
                    ViewedLastOnUTC,
                    NumberOfShares,
                    NumberOfViews,
                    Progress
                FROM
                    Stats_ContentUser
                WHERE
                    ContentID = @ContentID
                    AND UserID = @UserID
            ";

            var parameters = new MySqlParameter[]
            {
                new MySqlParameter("ContentID", contentId.ToString()),
                new MySqlParameter("UserID", userId.ToString()),
            };

            using var rdr = await sql.ReturnReader(query, parameters);
            if (await rdr.ReadAsync())
            {
                if (!rdr.IsDBNull(0))
                    record.LikedByUser = true;
                if (!rdr.IsDBNull(1))
                    record.SavedByUser = true;
                if (!rdr.IsDBNull(2))
                    record.ViewedByUser = true;
                record.ProgressByUser = rdr.IsDBNull(3) ? 0 : rdr.GetFloat(3);
            }

            return record;
        }
    }
}
