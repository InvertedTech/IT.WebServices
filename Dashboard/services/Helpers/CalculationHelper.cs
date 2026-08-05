using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Dashboard.Services.Helpers
{
    public static class CalculationHelper
    {
        public static double CalcPercentageChange(long current, long previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return (double)(current - previous) / previous * 100;
        }

        public static double CalcPercentageChange(double current, double previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return (current - previous) / previous * 100;
        }
    }
}
