using IT.WebServices.Fragments.Dashboard;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IT.WebServices.Dashboard.Services.Helpers;

namespace IT.WebServices.Dashboard.Services.Data
{
    internal class SqlUserDataProvider : IUserDataProvider
    {
        public readonly MySQLHelper sql;
        private readonly ILogger log;
        
        public SqlUserDataProvider(MySQLHelper sql, ILogger<SqlUserDataProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }

        public async Task<UserKpis> GetUserKpis()
        {
            try
            {
                // TODO: Make Sure Stuff Is Getting Pulled
                const string query = @"
                    SELECT
                      COUNT(*) AS total_current,
                      SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS new_current,
                      SUM(CASE WHEN DisabledOnUTC IS NOT NULL THEN 1 ELSE 0 END) AS disabled_current,
                      SUM(CASE WHEN CreatedOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS total_previous,
                      SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(DATE_SUB(NOW(), INTERVAL 1 MONTH), '%Y-%m-01') AND CreatedOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS new_previous,
                      SUM(CASE WHEN DisabledOnUTC IS NOT NULL AND DisabledOnUTC >= DATE_FORMAT(DATE_SUB(NOW(), INTERVAL 1 MONTH), '%Y-%m-01') AND DisabledOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS disabled_previous
                    FROM Auth_User
                ";

                await using var reader = await sql.ReturnReader(query);

                if (await reader.ReadAsync())
                {
                    var totalCurrent = reader.GetInt64(0);
                    var newCurrent = reader.GetInt64(1);
                    var disabledCurrent = reader.GetInt64(2);
                    var totalPrev = reader.GetInt64(3);
                    var newPrev = reader.GetInt64(4);
                    var disabledPrev = reader.GetInt64(5);

                    // TODO: Make Sure Calculation Works
                    var churnRate = totalCurrent > 0
                        ? (double)disabledCurrent / totalCurrent
                        : 0;

                    var churnPrev = totalPrev > 0
                        ? (double)disabledPrev / totalPrev
                        : 0;

                    return new UserKpis
                    {
                        TotalUsers = new CountComparison { CurrentCount = totalCurrent, PreviousCount = totalPrev, PercentageChange = CalculationHelper.CalcPercentageChange(totalCurrent, totalPrev) },
                        NewUsers = new CountComparison { CurrentCount = newCurrent, PreviousCount = newPrev, PercentageChange = CalculationHelper.CalcPercentageChange(newCurrent, newPrev)},
                        DisabledUsers = new CountComparison { CurrentCount = disabledCurrent, PreviousCount = disabledPrev , PercentageChange = CalculationHelper.CalcPercentageChange(disabledCurrent, disabledPrev) },
                        ChurnRate = new RatioComparison { CurrentRatio = churnRate, PreviousRatio = churnPrev, PercentageChange = CalculationHelper.CalcPercentageChange((long)churnRate, (long)churnPrev) },
                    };
                }

                return new UserKpis();
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message, ex);
                return null;
            }
        }
    }
}
