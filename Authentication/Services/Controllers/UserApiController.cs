using Microsoft.AspNetCore.Mvc;
using System;
using IT.WebServices.Fragments.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using IT.WebServices.Authentication.Services.Data;
using IT.WebServices.Authentication;
using IT.WebServices.Authentication.Services.Helpers;

namespace ON.Content.SimpleCMS.Service.Controllers
{
    [AllowAnonymous]
    [Route("/api/auth/user")]
    [ApiController]
    public class UserApiController : Controller
    {
        private readonly ILogger logger;
        private readonly IProfilePicDataProvider picProvider;

        public UserApiController(ILogger<UserApiController> logger, IProfilePicDataProvider picProvider)
        {
            this.logger = logger;
            this.picProvider = picProvider;
        }

        [HttpGet("{userID}/profileimage")]
        public async Task<IActionResult> GetUserProfileImage(string userID)
        {
            if (!Guid.TryParse(userID, out Guid recordId))
                return Redirect("/api/auth/noprofile.png");

            var bytes = await picProvider.GetById(recordId);
            if (bytes == null)
                return Redirect("/api/auth/noprofile.png");

            return File(bytes, "image/png");
        }

        [HttpGet("/api/auth/profileimage")]
        public async Task<IActionResult> GetMyUserProfileImage([FromServices] ONUserHelper userHelper)
        {
            Guid contentId = userHelper.MyUserId;
            if (contentId == Guid.Empty)
                return Redirect("/api/auth/noprofile.png");

            var bytes = await picProvider.GetById(contentId);
            if (bytes == null)
                return Redirect("/api/auth/noprofile.png");

            return File(bytes, "image/png");
        }

        [HttpGet("signed-qr")]
        public async Task<IActionResult> GetSignedQR([FromServices] SignedQRHelper qrHelper, [FromServices] ONUserHelper userHelper)
        {
            if (!userHelper.IsLoggedIn)
                return Unauthorized();

            var record = new UserQRRecord(userHelper.MyUserId, userHelper.MyUser.UserName, userHelper.MyUser.DisplayName, userHelper.MyUser.SubscriptionLevel);
            var bytes = qrHelper.GenerateSignedQR(record);
            return File(bytes, "image/png");
        }

        [HttpPost("verify-qr")]
        public IActionResult VerifySignedQR([FromServices] SignedQRHelper qrHelper, [FromBody] System.Text.Json.JsonElement body)
        {
            var valid = qrHelper.VerifySignedQR(body.GetRawText());
            if (!valid)
                return Unauthorized();
            return Ok();
        }
    }
}
