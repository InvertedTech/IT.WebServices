using System;
using System.Security.Cryptography;
using System.Text;

namespace IT.WebServices.Authentication.Services.Helpers
{
    public class ResetTokenHelper
    {
        public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

        public string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);

            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        public byte[] ComputeHash(string token)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(token));
        }

        public DateTime GetExpiresOnUTC()
        {
            return DateTime.UtcNow.Add(TokenLifetime);
        }
    }
}
