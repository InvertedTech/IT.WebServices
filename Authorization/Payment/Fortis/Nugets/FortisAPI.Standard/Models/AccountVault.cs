using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace FortisAPI.Standard.Models
{
    public class AccountVault
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("payment_method")]
        public string PaymentMethod { get; set; }

        [JsonProperty("account_holder_name")]
        public string AccountHolderName { get; set; }

        [JsonProperty("first_six")]
        public string CardFirstSix { get; set; }

        [JsonProperty("last_four")]
        public string CardLast4 { get; set; }

        [JsonProperty("card_bin")]
        public string CardBin { get; set; }

        [JsonProperty("exp_date")]
        public string CardExpirationDate { get; set; }

        [JsonProperty("created_ts")]
        public long CreatedTimestamp { get; set; }

        [JsonProperty("modified_ts")]
        public long ModifiedTimestamp { get; set; }

        [JsonIgnore]
        public uint CardExpYear
        {
            get
            {
                var date = CardExpirationDate ?? "";
                if (date.Length != 4)
                    return 0;

                var str = date.Substring(2, 2);
                var year = uint.Parse(str);

                return year;
            }
        }

        [JsonIgnore]
        public uint CardExpMonth
        {
            get
            {
                var date = CardExpirationDate ?? "";
                if (date.Length != 4)
                    return 0;

                var str = date.Substring(0, 2);
                var month = uint.Parse(str);

                if (month < 1)
                    return 0;

                if (month > 12)
                    return 0;

                return month;
            }
        }

        public override string ToString() => $"{CardLast4} - {CardExpMonth}/{CardExpYear}";
    }
}
