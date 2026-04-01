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
    [Authorize]
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

        [AllowAnonymous]
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

        [AllowAnonymous]
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
            var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL", EnvironmentVariableTarget.Process);
            var expDate = DateTime.UtcNow.AddMinutes(5);
            var record = new UserQRRecord(userHelper.MyUserId, userHelper.MyUser.UserName, userHelper.MyUser.DisplayName, userHelper.MyUser.SubscriptionLevel, expDate);
            var bytes = qrHelper.GenerateSignedQR(record, baseUrl);
            return File(bytes, "image/png");
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_EVENT_TICKET_MANAGER_OR_HIGHER)]
        [HttpGet("verify-qr")]
        public IActionResult VerifySignedQR([FromServices] SignedQRHelper qrHelper, [FromQuery] string token)
        {
            // TODO: Possibly Sign First+Last Names to token so that verification can be done on the name front as well
            var redirectBase = Environment.GetEnvironmentVariable("QR_CODE_ADMIN_REDIRECT", EnvironmentVariableTarget.Process);
            var res = qrHelper.VerifySignedQR(token);
            if (res == null)
            {
                var reason = "No Response";
                return Redirect($"{redirectBase}?valid=false&name=null&level=null&reason={reason}");
            }

            if (res.Record == null)
                return Redirect($"{redirectBase}?valid=false&name=null&level=null&reason={res.Reason}");

            if (res.IsValid == false)
                return Redirect($"{redirectBase}?valid=false&name={Uri.EscapeDataString(res.Record.DisplayName)}&level={res.Record.SubscriptionLevelCents}&reason={res.Reason}");

            return Redirect($"{redirectBase}?valid=true&name={Uri.EscapeDataString(res.Record.DisplayName)}&level={res.Record.SubscriptionLevelCents}");
        }
    }
}
