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
                const string query = @"
                    SELECT
                      COUNT(*) AS total_current,
                      CAST(COALESCE(SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(UTC_TIMESTAMP(), '%Y-%m-01') THEN 1 ELSE 0 END), 0) AS SIGNED) AS new_current,
                      CAST(COALESCE(SUM(CASE WHEN DisabledOnUTC >= DATE_FORMAT(UTC_TIMESTAMP(), '%Y-%m-01') THEN 1 ELSE 0 END), 0) AS SIGNED) AS disabled_current,
                      CAST(COALESCE(SUM(CASE WHEN CreatedOnUTC < DATE_FORMAT(UTC_TIMESTAMP(), '%Y-%m-01') THEN 1 ELSE 0 END), 0) AS SIGNED) AS total_previous,
                      CAST(COALESCE(SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 MONTH), '%Y-%m-01') AND CreatedOnUTC < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 MONTH) THEN 1 ELSE 0 END), 0) AS SIGNED) AS new_previous,
                      CAST(COALESCE(SUM(CASE WHEN DisabledOnUTC >= DATE_FORMAT(DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 MONTH), '%Y-%m-01') AND DisabledOnUTC < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 MONTH) THEN 1 ELSE 0 END), 0) AS SIGNED) AS disabled_previous,
                      CAST(COALESCE(SUM(CASE WHEN CreatedOnUTC < DATE_FORMAT(DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 MONTH), '%Y-%m-01') THEN 1 ELSE 0 END), 0) AS SIGNED) AS total_at_prev_month_start,
                      CAST(COALESCE(SUM(CASE WHEN DisabledOnUTC < DATE_FORMAT(UTC_TIMESTAMP(), '%Y-%m-01') THEN 1 ELSE 0 END), 0) AS SIGNED) AS disabled_before_this_month,
                      CAST(COALESCE(SUM(CASE WHEN DisabledOnUTC < DATE_FORMAT(DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 MONTH), '%Y-%m-01') THEN 1 ELSE 0 END), 0) AS SIGNED) AS disabled_before_prev_month
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
                    var totalAtPrevMonthStart = reader.GetInt64(6);
                    var disabledBeforeThisMonth = reader.GetInt64(7);
                    var disabledBeforePrevMonth = reader.GetInt64(8);

                    // Churn = disabled during the month / active users at the start of the month
                    var currentBase = totalPrev - disabledBeforeThisMonth;
                    var prevBase = totalAtPrevMonthStart - disabledBeforePrevMonth;

                    var churnRate = currentBase > 0
                        ? (double)disabledCurrent / currentBase
                        : 0;

                    var churnPrev = prevBase > 0
                        ? (double)disabledPrev / prevBase
                        : 0;

                    return new UserKpis
                    {
                        TotalUsers = new CountComparison { CurrentCount = totalCurrent, PreviousCount = totalPrev, PercentageChange = CalculationHelper.CalcPercentageChange(totalCurrent, totalPrev) },
                        NewUsers = new CountComparison { CurrentCount = newCurrent, PreviousCount = newPrev, PercentageChange = CalculationHelper.CalcPercentageChange(newCurrent, newPrev)},
                        DisabledUsers = new CountComparison { CurrentCount = disabledCurrent, PreviousCount = disabledPrev , PercentageChange = CalculationHelper.CalcPercentageChange(disabledCurrent, disabledPrev) },
                        ChurnRate = new RatioComparison { CurrentRatio = churnRate, PreviousRatio = churnPrev, PercentageChange = CalculationHelper.CalcPercentageChange(churnRate, churnPrev) },
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
