using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Helpers
{
    public static class StringExtensions
    {
        public static string TruncateIfTooLong(this string str, int maxLength)
        {
            if (str.Length <= maxLength)
                return str;

            return str.Substring(0, maxLength);
        }
    }
}
