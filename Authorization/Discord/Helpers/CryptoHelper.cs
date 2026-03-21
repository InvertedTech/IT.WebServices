using Microsoft.IdentityModel.Tokens;
using NSec.Cryptography;
using System.Text;

namespace IT.WebServices.Authorization.Discord.Helpers
{
    public static class CryptoHelper
    {
        public static string GenerateHmacSha256State(string platformUserId, string stateSecret)
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(stateSecret);
            byte[] data = Encoding.UTF8.GetBytes(platformUserId);

            using var key = Key.Import(MacAlgorithm.HmacSha256, secretBytes, KeyBlobFormat.RawSymmetricKey);
            byte[] hmac = MacAlgorithm.HmacSha256.Mac(key, data);

            string combined = Convert.ToHexString(hmac) + ":" + platformUserId;
            byte[] combinedBytes = Encoding.UTF8.GetBytes(combined);

            return Base64UrlEncoder.Encode(combinedBytes);
        }
    }
}
