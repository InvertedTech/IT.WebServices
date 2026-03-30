using IT.WebServices.Helpers;
using IT.WebServices.Merch.Combined.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Merch.Combined.Controllers
{
    [AllowAnonymous]
    [Route("/api/settings/merch/stores")]
    [ApiController]
    public class StoreApiController : Controller
    {
        private readonly SettingsHelper settings;
        private readonly ILogger log;

        public StoreApiController(SettingsHelper settings, ILogger<StoreApiController> log)
        {
            this.settings = settings;
            this.log = log;
        }

        [AllowAnonymous]
        [HttpGet("")]
        public async Task<IActionResult> GetStores()
        {
            var stores = new List<PublicStore>();
            var owner = settings.Owner.Merch.Shopify;

            foreach (var store in owner.Stores)
            {
                Guid.TryParse(store.InternalStoreID, out var storeID);
                if (storeID == Guid.Empty)
                {
                    continue;
                }
                var pubStore = new PublicStore
                {
                    StoreId = storeID,
                    StoreName = store.StoreName,
                };

                stores.Add(pubStore);
            }

            return Ok(stores);
        }
    }
}
