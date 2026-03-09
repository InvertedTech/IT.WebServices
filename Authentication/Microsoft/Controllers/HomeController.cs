using IT.WebServices.Authentication.Services.Data;
using IT.WebServices.Authentication.Services.Helpers;
using IT.WebServices.Authentication.Services.Microsoft.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Web;

namespace IT.WebServices.Authentication.Services.Microsoft.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly MySettings settings;
        private readonly TokenHelper tokenHelper;
        private readonly IUserDataProvider userDataProvider;
        private readonly ILogger log;

        public HomeController(IOptions<MySettings> settings, TokenHelper tokenHelper, IUserDataProvider userDataProvider, ILogger<HomeController> log)
        {
            this.settings = settings.Value;
            this.tokenHelper = tokenHelper;
            this.userDataProvider = userDataProvider;
            this.log = log;
        }

        public async Task<IActionResult> Index()
        {
            var claims = (User?.Identity as ClaimsIdentity)?.Claims?.ToList() ?? new();
            foreach (var c in claims)
                log.LogInformation("Claim: {Type} = {value}", c.Type, c.Value);

            var azureId = GetUserAzureId();

            if (azureId == Guid.Empty)
                return Unauthorized();

            var userId = await userDataProvider.GetIdByMicrosoftAuthProviderUserId(azureId.ToString());
            log.LogInformation("UserId: {userId}", userId.ToString());
            if (userId == Guid.Empty)
                return Unauthorized();

            var token = await tokenHelper.GenerateToken(userId);
            log.LogInformation("Token: {token}", token);
            if (string.IsNullOrEmpty(token))
                return Unauthorized();

            var uriBuilder = new UriBuilder(settings.GoodRedirect);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            Response.Cookies.Append(JwtExtensions.JWT_COOKIE_NAME, token, new CookieOptions()
            {
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddDays(21),
                IsEssential = true,
                Domain = GetMainDomain(),
            });

            switch (settings.CookieOrGet)
            {
                case MySettings.CookieOrGetEnum.Cookie:
                    break;
                case MySettings.CookieOrGetEnum.Get:
                    query["token"] = token;
                    break;
            }

            uriBuilder.Query = query.ToString();

            return Redirect(uriBuilder.Uri.ToString());
        }

        private string GetMainDomain()
        {
            var host = Request.Host.Host;

            var pieces = host.Split('.');
            if (pieces.Length <= 2)
                return host;

            return string.Join('.', pieces.Skip(pieces.Length - 2));
        }

        public Guid GetUserAzureId()
        {
            var str = (User?.Identity as ClaimsIdentity)?.Claims?.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            log.LogInformation("Azure ID: {azureId}", str);

            if (str is null)
                return Guid.Empty;

            if (Guid.TryParse(str, out var guid))
                return guid;

            return Guid.Empty;
        }
    }
}
