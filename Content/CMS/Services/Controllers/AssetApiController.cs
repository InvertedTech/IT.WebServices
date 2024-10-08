﻿using Microsoft.AspNetCore.Mvc;
using System;
using IT.WebServices.Fragments.Generic;
using Microsoft.Extensions.Logging;
using IT.WebServices.Content.CMS.Services.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using IT.WebServices.Content.CMS.Services.Models;

namespace IT.WebServices.Content.CMS.Services.Controllers
{
    [AllowAnonymous]
    [Route("/api/cms/asset")]
    [ApiController]
    public class AssetApiController : Controller
    {
        private readonly ILogger logger;
        private readonly IAssetDataProvider dataProvider;

        public AssetApiController(ILogger<AssetApiController> logger, IAssetDataProvider dataProvider)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
        }

        [HttpGet("{assetID}/data")]
        public async Task<IActionResult> GetAssetPublic(string assetID)
        {
            Guid contentId = assetID.ToGuid();
            if (contentId == Guid.Empty)
                return NotFound();

            var rec = await dataProvider.GetById(contentId);
            if (rec == null)
                return NotFound();

            if (rec.Audio != null)
                return File(rec.Audio.Public.Data.Data.ToByteArray(), rec.Audio.Public.Data.MimeType);

            if (rec.Image != null)
                return File(rec.Image.Public.Data.Data.ToByteArray(), rec.Image.Public.Data.MimeType);

            return NotFound();
        }

        [HttpPost("audio")]
        public IActionResult UploadAudio(UploadAudioRequest req)
        {

            return NotFound();
        }
    }
}
