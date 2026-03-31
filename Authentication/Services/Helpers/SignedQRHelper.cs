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

        // TODO: Import keys from config
        public SignedQRHelper(ILogger<SignedQRHelper> log)
        {
            privateKey = JwtExtensions.GetPrivateKey().ToECDsa();
            publicKey = JwtExtensions.GetPublicKey().ToECDsa();
            this.log = log;

            options = new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        }

        public byte[] GenerateSignedQR(UserQRRecord record)
        {
            string json = JsonSerializer.Serialize(record);
            var data = Encoding.UTF8.GetBytes(json);
            var signature = privateKey.SignData(data, HashAlgorithmName.SHA256);

            var payload = new DataQRRecord(json, Convert.ToBase64String(signature));
            var qrContent = JsonSerializer.Serialize(payload, options);

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            return qrCode.GetGraphic(10);
        }

        public bool VerifySignedQR(string scannedContent)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<DataQRRecord>(scannedContent);

                byte[] dataBytes = Encoding.UTF8.GetBytes(payload.data);
                byte[] signature = Convert.FromBase64String(payload.sig);

                if (!publicKey.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256))
                    return false;

                var record = JsonSerializer.Deserialize<UserQRRecord>(payload.data);
                if (record.ExpiresOnUTC < DateTime.UtcNow)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to verify QR code");
                return false;
            }
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
}
