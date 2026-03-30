using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Merch.Combined.Models
{
    public class PublicStore
    {
        public Guid StoreId { get; set; } = Guid.Empty;
        public string StoreName { get; set; } = string.Empty;
    }
}
