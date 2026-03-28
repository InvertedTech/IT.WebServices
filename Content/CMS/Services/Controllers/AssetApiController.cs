using IT.WebServices.Authentication;
using IT.WebServices.Content.CMS.Services.Data;
using IT.WebServices.Content.CMS.Services.Models;
using IT.WebServices.Fragments.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace IT.WebServices.Content.CMS.Services.Controllers
{
    [Authorize(Roles = RoleAbilities.ROLE_CAN_CREATE_CONTENT)]
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

        [AllowAnonymous]
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

        [Authorize(Roles = RoleAbilities.ROLE_CAN_CREATE_CONTENT)]
        [HttpPost("audio")]
        public IActionResult UploadAudio(UploadAudioRequest req)
        {

            return NotFound();
        }
    }
}
