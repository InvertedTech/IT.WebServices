namespace FortisAPI.Standard.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using FortisAPI.Standard;
    using FortisAPI.Standard.Utilities;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class ResponseDeclinedRecurringTransactionData
    {
        public ResponseDeclinedRecurringTransactionData()
        {
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("declined_transaction_id")]
        public string DeclinedTransactionId { get; set; }

        [JsonProperty("payment_transaction_id")]
        public string PaymentTransactionId { get; set; }

        [JsonProperty("status")]
        public Models.StatusId2Enum Status { get; set; }

        [JsonProperty("recurring_id")]
        public string RecurringId { get; set; }

        [JsonProperty("created_ts")]
        public long CreatedTs { get; set; }

        [JsonProperty("created_user_id")]
        public string CreatedUserId { get; set; }

        [JsonProperty("modified_ts")]
        public long? ModifiedTs { get; set; }

        [JsonProperty("modified_user_id")]
        public string ModifiedUserId { get; set; }
    }
}