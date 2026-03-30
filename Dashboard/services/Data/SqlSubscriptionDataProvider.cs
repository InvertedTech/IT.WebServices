using IT.WebServices.Fragments.Dashboard;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using IT.WebServices.Dashboard.Services.Helpers;

namespace IT.WebServices.Dashboard.Services.Data
{
    internal class SqlSubscriptionDataProvider : ISubscriptionDataProvider
    {
        public readonly MySQLHelper sql;
        private readonly ILogger log;

        public SqlSubscriptionDataProvider(MySQLHelper sql, ILogger<SqlSubscriptionDataProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }

        public async Task<SubscriptionKpis> GetSubscriptionKpis()
        {
            try
            {
                const string query = @"
                    SELECT
                        SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS active_subs_current,
                        SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS new_subs_current,
                        SUM(CASE WHEN CanceledOnUTC IS NOT NULL AND CanceledOnUTC >= DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS canceled_subs_current,
                        SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(NOW(), '%Y-%m-01') THEN TotalCents ELSE 0 END) AS new_revenue_current,
                        SUM(CASE WHEN Status = 2 THEN TotalCents ELSE 0 END) AS total_revenue_current,
                        SUM(CASE WHEN Status = 2 AND CreatedOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS active_subs_previous,
                        SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(DATE_SUB(NOW(), INTERVAL 1 MONTH), '%Y-%m-01') AND CreatedOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS new_subs_previous,
                        SUM(CASE WHEN CanceledOnUTC IS NOT NULL AND CanceledOnUTC >= DATE_FORMAT(DATE_SUB(NOW(), INTERVAL 1 MONTH), '%Y-%m-01') AND CanceledOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) AS canceled_subs_previous,
                        SUM(CASE WHEN CreatedOnUTC >= DATE_FORMAT(DATE_SUB(NOW(), INTERVAL 1 MONTH), '%Y-%m-01') AND CreatedOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN TotalCents ELSE 0 END) AS new_revenue_previous,
                        SUM(CASE WHEN Status = 2 AND CreatedOnUTC < DATE_FORMAT(NOW(), '%Y-%m-01') THEN TotalCents ELSE 0 END) AS total_revenue_previous
                    FROM Payment_Generic_Subscription
                ";

                await using var reader = await sql.ReturnReader(query);

                if (await reader.ReadAsync())
                {
                    var activeSubsCurrent = reader.GetInt64(0);
                    var newSubsCurrent = reader.GetInt64(1);
                    var canceledSubsCurrent = reader.GetInt64(2);
                    var newRevenueCurrent = reader.GetInt64(3);
                    var totalRevenueCurrent = reader.GetInt64(4);
                    var activeSubsPrevious = reader.GetInt64(5);
                    var newSubsPrevious = reader.GetInt64(6);
                    var canceledSubsPrevious = reader.GetInt64(7);
                    var newRevenuePrevious = reader.GetInt64(8);
                    var totalRevenuePrevious = reader.GetInt64(9);

                    return new SubscriptionKpis
                    {
                        ActiveSubscriptions = new CountComparison
                        {
                            CurrentCount = activeSubsCurrent,
                            PreviousCount = activeSubsPrevious,
                            PercentageChange = CalculationHelper.CalcPercentageChange(activeSubsCurrent, activeSubsPrevious),
                        },
                        NewSubscriptions = new CountComparison
                        {
                            CurrentCount = newSubsCurrent,
                            PreviousCount = newSubsPrevious,
                            PercentageChange = CalculationHelper.CalcPercentageChange(newSubsCurrent, newSubsPrevious),
                        },
                        CanceledSubscriptions = new CountComparison
                        {
                            CurrentCount = canceledSubsCurrent,
                            PreviousCount = canceledSubsPrevious,
                            PercentageChange = CalculationHelper.CalcPercentageChange(canceledSubsCurrent, canceledSubsPrevious),
                        },
                        NewSubscriptionRevenue = new CentsComparison
                        {
                            CurrentCents = newRevenueCurrent,
                            PreviousCents = newRevenuePrevious,
                            PercentageChange = CalculationHelper.CalcPercentageChange(newRevenueCurrent, newRevenuePrevious),
                        },
                        TotalSubscriptionRevenue = new CentsComparison
                        {
                            CurrentCents = totalRevenueCurrent,
                            PreviousCents = totalRevenuePrevious,
                            PercentageChange = CalculationHelper.CalcPercentageChange(totalRevenueCurrent, totalRevenuePrevious),
                        },
                    };
                }

                return new SubscriptionKpis();
            } catch (Exception ex)
            {
                log.LogError(ex.Message, ex);
                return null;
            }
        }
    }
}
