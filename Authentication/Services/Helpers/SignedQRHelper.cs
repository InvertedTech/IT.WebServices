using IT.WebServices.Crypto;
using Microsoft.Extensions.Logging;
using QRCoder;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace IT.WebServices.Authentication.Services.Helpers
{
    public class SignedQRHelper
    {
        private readonly ECDsa privateKey;
        private readonly ECDsa publicKey;
        private readonly ILogger log;
        private readonly JsonSerializerOptions options;

        private readonly int minimumSubLevel;

        public SignedQRHelper(ILogger<SignedQRHelper> log)
        {
            privateKey = JwtExtensions.GetPrivateKey().ToECDsa();
            publicKey = JwtExtensions.GetPublicKey().ToECDsa();
            this.log = log;

            options = new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            
            var str = Environment.GetEnvironmentVariable("QR_CODE_MINIMUM_SUB_LEVEL", EnvironmentVariableTarget.Process) ?? string.Empty;
            int.TryParse(str, out minimumSubLevel);
        }

        public byte[] GenerateSignedQR(UserQRRecord record, string baseUrl)
        {
            string json = JsonSerializer.Serialize(record, options);
            var data = Encoding.UTF8.GetBytes(json);
            var signature = privateKey.SignData(data, HashAlgorithmName.SHA256);

            var payload = new DataQRRecord(json, Convert.ToBase64String(signature));
            var token = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, options)));
            var qrContent = $"{baseUrl}/api/auth/user/verify-qr?token={token}";

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            return qrCode.GetGraphic(10);
        }

        public QRVerificationResult VerifySignedQR(string token)
        {
            try
            {
                var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(token));
                var payload = JsonSerializer.Deserialize<DataQRRecord>(payloadJson);

                if (payload is null)
                    return new QRVerificationResult(false, "Invalid QR", null);

                byte[] dataBytes = Encoding.UTF8.GetBytes(payload.data);
                byte[] signature = Convert.FromBase64String(payload.sig);

                if (!publicKey.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256))
                    return new QRVerificationResult(false, "Invalid Signature", null);

                var record = JsonSerializer.Deserialize<UserQRRecord>(payload.data);
                if (record is null)
                    return new QRVerificationResult(false, "Invalid QR", null);
                if (record.ExpiresOnUTC < DateTime.UtcNow)
                    return new QRVerificationResult(false, "Expired QR", record);

                var minimumSub = minimumSubLevel;
                if (record.SubscriptionLevelCents < minimumSub)
                    return new QRVerificationResult(false, $"Subscription Level Below Minimum Of {minimumSub}", record);

                return new QRVerificationResult(true, "", record);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to verify QR code");
                return new QRVerificationResult(false, "Unknown error", null);
            }
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static byte[] Base64UrlDecode(string value)
        {
            value = value.Replace('-', '+').Replace('_', '/');
            switch (value.Length % 4)
            {
                case 2: value += "=="; break;
                case 3: value += "="; break;
            }
            return Convert.FromBase64String(value);
        }
    }

    public record DataQRRecord(
        string data,
        string sig
    );

    public record UserQRRecord(
        Guid UserId,
        string UserName,
        string DisplayName,
        uint SubscriptionLevelCents,
        DateTime ExpiresOnUTC
    );

    public record QRVerificationResult(
        bool IsValid,
        string Reason,
        UserQRRecord? Record
    );
}
