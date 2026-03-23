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

        // Generates a state token for the Sign-in with Discord flow (no platform user ID)
        public static string GenerateSignInState(string stateSecret)
            => GenerateHmacSha256State("signin", stateSecret);

        // Decodes and validates a state token.
        // Returns true if the HMAC is valid.
        // platformUserId will be null for sign-in flow, or a Guid string for the link flow.
        public static bool TryValidateState(string? state, string stateSecret, out string? platformUserId)
        {
            platformUserId = null;

            if (string.IsNullOrEmpty(state))
                return false;

            try
            {
                var decoded = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(state));
                var separatorIndex = decoded.IndexOf(':');
                if (separatorIndex < 0)
                    return false;

                var receivedHmacHex = decoded[..separatorIndex];
                var payload = decoded[(separatorIndex + 1)..];

                var expected = GenerateHmacSha256State(payload, stateSecret);
                var expectedDecoded = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(expected));
                var expectedHmacHex = expectedDecoded[..expectedDecoded.IndexOf(':')];

                if (!string.Equals(receivedHmacHex, expectedHmacHex, StringComparison.OrdinalIgnoreCase))
                    return false;

                platformUserId = payload == "signin" ? null : payload;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
