using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Models
{
    public class AppSettings
    {
        public string DataStore { get; set; } = "/data";
        public string MySQLConn { get; set; } = "server=127.0.0.1;database=tmpdata;user=root;password=password";
        public string FortisDeveloperId { get; set; } = "IphR7xVH";
    }
}