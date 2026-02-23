using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.Dashboard;
using System;

namespace IT.WebServices.Dashboard.Services
{
    public interface IKpiMockDataProvider
    {
        GetKpisResponse CreateResponse();
    }

    public class KpiMockDataProvider : IKpiMockDataProvider
    {
        public GetKpisResponse CreateResponse()
        {
            return new GetKpisResponse
            {
                AsOfUTC = Timestamp.FromDateTime(DateTime.UtcNow),
                Users = new UserKpis
                {
                    TotalUsers = new CountComparison
                    {
                        CurrentCount = 12450,
                        PreviousCount = 11820,
                        PercentageChange = 5.33
                    },
                    NewUsers = new CountComparison
                    {
                        CurrentCount = 386,
                        PreviousCount = 342,
                        PercentageChange = 12.87
                    },
                    DisabledUsers = new CountComparison
                    {
                        CurrentCount = 42,
                        PreviousCount = 37,
                        PercentageChange = 13.51
                    },
                    ChurnRate = new RatioComparison
                    {
                        CurrentRatio = 0.029,
                        PreviousRatio = 0.031,
                        PercentageChange = -6.45
                    }
                },
                Subscriptions = new SubscriptionKpis
                {
                    ActiveSubscriptions = new CountComparison
                    {
                        CurrentCount = 3810,
                        PreviousCount = 3640,
                        PercentageChange = 4.67
                    },
                    NewSubscriptions = new CountComparison
                    {
                        CurrentCount = 228,
                        PreviousCount = 201,
                        PercentageChange = 13.43
                    },
                    CanceledSubscriptions = new CountComparison
                    {
                        CurrentCount = 61,
                        PreviousCount = 73,
                        PercentageChange = -16.44
                    },
                    NewSubscriptionRevenue = new CentsComparison
                    {
                        CurrentCents = 942500,
                        PreviousCents = 811300,
                        PercentageChange = 16.17
                    },
                    TotalSubscriptionRevenue = new CentsComparison
                    {
                        CurrentCents = 12984500,
                        PreviousCents = 12111000,
                        PercentageChange = 7.21
                    },
                    TopPlansByRevenue =
                    {
                        new TopPlanByRevenue
                        {
                            PlanId = "pro-monthly",
                            PlanName = "Pro Monthly",
                            RevenueCents = 5120000
                        },
                        new TopPlanByRevenue
                        {
                            PlanId = "team-annual",
                            PlanName = "Team Annual",
                            RevenueCents = 4115000
                        },
                        new TopPlanByRevenue
                        {
                            PlanId = "starter-monthly",
                            PlanName = "Starter Monthly",
                            RevenueCents = 2129500
                        }
                    }
                },
                Content = new ContentKpis
                {
                    UniqueViewers = new CountComparison
                    {
                        CurrentCount = 7340,
                        PreviousCount = 6895,
                        PercentageChange = 6.45
                    },
                    CompletionRate = new RatioComparison
                    {
                        CurrentRatio = 0.612,
                        PreviousRatio = 0.587,
                        PercentageChange = 4.26
                    },
                    MedianCompletionPercent = new RatioComparison
                    {
                        CurrentRatio = 0.71,
                        PreviousRatio = 0.66,
                        PercentageChange = 7.58
                    },
                    AvgConsumptionSeconds = new RatioComparison
                    {
                        CurrentRatio = 248.3,
                        PreviousRatio = 226.7,
                        PercentageChange = 9.53
                    },
                    TopLikedContent =
                    {
                        new TopLikedContentItem
                        {
                            ContentId = "c_1001",
                            ContentTitle = "How to Build Better Prompts",
                            LikeCount = 842
                        },
                        new TopLikedContentItem
                        {
                            ContentId = "c_1017",
                            ContentTitle = "Advanced Workflow Automation",
                            LikeCount = 719
                        },
                        new TopLikedContentItem
                        {
                            ContentId = "c_0992",
                            ContentTitle = "Scaling Teams Without Burnout",
                            LikeCount = 655
                        }
                    }
                }
            };
        }
    }
}
