using Grpc.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading;

namespace IT.WebServices.Authentication
{
    public class ONUserHelper
    {
        public readonly ONUser MyUser;
        public readonly bool IsLoggedIn;
        public readonly Guid MyUserId;

        public ONUserHelper(IHttpContextAccessor httpContextAccessor)
        {
            MyUser = ParseUser(httpContextAccessor.HttpContext);
            IsLoggedIn = MyUser != null;
            MyUserId = MyUser?.Id ?? Guid.Empty;
        }

        public CallOptions GetGrpcCallOptions(CancellationToken cancellationToken = default) => MyUser?.GetGrpcCallOptions(cancellationToken) ?? new(new Metadata(), cancellationToken: cancellationToken);

        public static ONUser ParseUser(HttpContext context)
        {
            var user = ONUser.Parse(context.User.Claims.ToArray());
            if (user != null)
                user.JwtToken = GrabToken(context);

            return user;
        }

        private static string GrabToken(HttpContext context)
        {
            string cookie = context.Request.Cookies[JwtExtensions.JWT_COOKIE_NAME];
            if (!string.IsNullOrWhiteSpace(cookie))
                return cookie;

            string authorization = context.Request.Headers["Authorization"];

            if (string.IsNullOrWhiteSpace(authorization))
                return "";

            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return "";

            return authorization.Substring(7).Trim();
        }
    }
}
