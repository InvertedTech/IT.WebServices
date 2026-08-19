using System.Collections.Generic;
using Newtonsoft.Json;

namespace FortisAPI.Standard.Models
{
    /// <summary>
    /// ResponseDeclinedRecurringTransactionsCollection.
    /// </summary>
    public class ResponseDeclinedRecurringTransactionsCollection
    {
        public ResponseDeclinedRecurringTransactionsCollection()
        {
        }

        /// <summary>
        /// Resource Type
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Resource Members
        /// </summary>
        [JsonProperty("list")]
        public List<Models.ResponseDeclinedRecurringTransaction> List { get; set; }

        public override string ToString()
        {
            var toStringOutput = new List<string>();

            this.ToString(toStringOutput);

            return $"ResponseDeclinedRecurringTransactionsCollection : ({string.Join(", ", toStringOutput)})";
        }
        

        /// <summary>
        /// ToString overload.
        /// </summary>
        /// <param name="toStringOutput">List of strings.</param>
        protected void ToString(List<string> toStringOutput)
        {
            toStringOutput.Add($"this.Type = {(this.Type == null ? "null" : this.Type == string.Empty ? "" : this.Type)}");
            toStringOutput.Add($"this.List = {(this.List == null ? "null" : $"[{string.Join(", ", this.List)} ]")}");
        }
    }
}