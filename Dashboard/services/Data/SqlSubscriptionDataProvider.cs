using IT.WebServices.Fragments.Dashboard;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;          // ← NEW (for DateTime.ParseExact)
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes; // ← NEW (for Timestamp)
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
                // ==================== 1. Current vs Previous month KPIs (your original query) ====================
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

                var kpis = new SubscriptionKpis();

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

                    kpis.ActiveSubscriptions = new CountComparison
                    {
                        CurrentCount = activeSubsCurrent,
                        PreviousCount = activeSubsPrevious,
                        PercentageChange = CalculationHelper.CalcPercentageChange(activeSubsCurrent, activeSubsPrevious),
                    };

                    kpis.NewSubscriptions = new CountComparison
                    {
                        CurrentCount = newSubsCurrent,
                        PreviousCount = newSubsPrevious,
                        PercentageChange = CalculationHelper.CalcPercentageChange(newSubsCurrent, newSubsPrevious),
                    };

                    kpis.CanceledSubscriptions = new CountComparison
                    {
                        CurrentCount = canceledSubsCurrent,
                        PreviousCount = canceledSubsPrevious,
                        PercentageChange = CalculationHelper.CalcPercentageChange(canceledSubsCurrent, canceledSubsPrevious),
                    };

                    kpis.NewSubscriptionRevenue = new CentsComparison
                    {
                        CurrentCents = newRevenueCurrent,
                        PreviousCents = newRevenuePrevious,
                        PercentageChange = CalculationHelper.CalcPercentageChange(newRevenueCurrent, newRevenuePrevious),
                    };

                    kpis.TotalSubscriptionRevenue = new CentsComparison
                    {
                        CurrentCents = totalRevenueCurrent,
                        PreviousCents = totalRevenuePrevious,
                        PercentageChange = CalculationHelper.CalcPercentageChange(totalRevenueCurrent, totalRevenuePrevious),
                    };
                }

                // ==================== 2. Month-over-month New + Canceled + Total Revenue Series ====================
                // (Exactly the code you asked me to insert – now cleanly placed after the KPIs)
                const string seriesQuery = @"
                    SELECT 
                        ds.bucket_start,
                        COUNT(CASE WHEN DATE_FORMAT(s.CreatedOnUTC, '%Y-%m-01') = ds.bucket_start THEN 1 END) AS new_value,
                        COUNT(CASE WHEN DATE_FORMAT(s.CanceledOnUTC, '%Y-%m-01') = ds.bucket_start THEN 1 END) AS canceled_value,
                        SUM(CASE WHEN DATE_FORMAT(s.CreatedOnUTC, '%Y-%m-01') = ds.bucket_start THEN COALESCE(s.AmountCents, 0) ELSE 0 END) AS revenue_value
                    FROM (
                        SELECT 
                            DATE_FORMAT(
                                DATE_ADD(
                                    DATE_FORMAT(DATE_SUB(NOW(), INTERVAL 12 MONTH), '%Y-%m-01'),
                                    INTERVAL t.n MONTH
                                ),
                                '%Y-%m-01'
                            ) AS bucket_start
                        FROM (
                            SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3
                            UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7
                            UNION ALL SELECT 8 UNION ALL SELECT 9 UNION ALL SELECT 10
                            UNION ALL SELECT 11 UNION ALL SELECT 12
                        ) t
                    ) ds
                    LEFT JOIN Payment_Generic_Subscription s 
                        ON DATE_FORMAT(s.CreatedOnUTC, '%Y-%m-01') = ds.bucket_start 
                        OR (DATE_FORMAT(s.CanceledOnUTC, '%Y-%m-01') = ds.bucket_start AND s.CanceledOnUTC IS NOT NULL)
                    GROUP BY ds.bucket_start
                    ORDER BY ds.bucket_start ASC
                ";

                await using var seriesReader = await sql.ReturnReader(seriesQuery);

                var newSeriesList = new List<CountSeriesPoint>();
                var canceledSeriesList = new List<CountSeriesPoint>();
                var revenueSeriesList = new List<CentsSeriesPoint>();

                while (await seriesReader.ReadAsync())
                {
                    var bucketStartStr = seriesReader.GetString(0);
                    var newValue = seriesReader.GetInt64(1);
                    var canceledValue = seriesReader.GetInt64(2);
                    var revenueValue = seriesReader.GetInt64(3);

                    // Convert MySQL YYYY-MM-01 string to protobuf Timestamp (UTC)
                    var bucketStartDt = DateTime.ParseExact(bucketStartStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var bucketStartTs = Timestamp.FromDateTime(DateTime.SpecifyKind(bucketStartDt, DateTimeKind.Utc));

                    // BucketEnd = first day of next month (exclusive end of bucket)
                    var nextMonthDt = bucketStartDt.AddMonths(1);
                    var bucketEndTs = Timestamp.FromDateTime(DateTime.SpecifyKind(nextMonthDt, DateTimeKind.Utc));

                    // New Subscriptions series
                    newSeriesList.Add(new CountSeriesPoint
                    {
                        BucketStart = bucketStartTs,
                        BucketEnd = bucketEndTs,
                        Value = newValue
                    });

                    // Canceled Subscriptions series
                    canceledSeriesList.Add(new CountSeriesPoint
                    {
                        BucketStart = bucketStartTs,
                        BucketEnd = bucketEndTs,
                        Value = canceledValue
                    });

                    // Total Revenue series (using AmountCents column)
                    revenueSeriesList.Add(new CentsSeriesPoint
                    {
                        BucketStart = bucketStartTs,
                        BucketEnd = bucketEndTs,
                        ValueCents = revenueValue
                    });
                }

                kpis.NewSubscriptionsSeries.AddRange(newSeriesList);
                kpis.CanceledSubscriptionsSeries.AddRange(canceledSeriesList);
                kpis.TotalRevenueSeries.AddRange(revenueSeriesList);

                return kpis;
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message, ex);
                return null;
            }
        }
    }
}