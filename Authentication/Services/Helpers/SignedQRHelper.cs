using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.Authentication;
using Microsoft.Extensions.Logging;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IT.WebServices.Authentication.Services.Helpers
{
    public class SignedQRHelper
    {
        private readonly ECDsa _privateKey;
        private readonly ECDsa _publicKey;
        private readonly ILogger log;

        // TODO: Import keys from config
        public SignedQRHelper(ECDsa privateKey, ECDsa publicKey, ILogger<SignedQRHelper> log)
        {
            _privateKey = privateKey;
            _publicKey = publicKey;
            this.log = log;
        }

        public byte[] GenerateSignedQR(UserQRRecord record)
        {
            string json = JsonSerializer.Serialize(record);
            var data = Encoding.UTF8.GetBytes(json);
            var signature = _privateKey.SignData(data, HashAlgorithmName.SHA256);

            var payload = new
            {
                data = json,
                sig = Convert.ToBase64String(signature)
            };
            var qrContent = JsonSerializer.Serialize(payload);
            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            return qrCode.GetGraphic(10);
        }

        public bool VerifySignedQR(string scannedContent)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(scannedContent);
                string data = payload.GetProperty("data").GetString();
                string sigBase64 = payload.GetProperty("sig").GetString();

                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] signature = Convert.FromBase64String(sigBase64);

                if (!_publicKey.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256))
                    return false;

                var record = JsonSerializer.Deserialize<UserQRRecord>(data);
                if (record.ExpiresOnUTC < DateTime.UtcNow)
                    return false;

                return true;
            } catch (Exception ex)
            {
                log.LogError(ex, "Failed to verify QR code");
                return false;
            }
        }
    }

    public record UserQRRecord(
        Guid UserId,
        string UserName, 
        string DisplayName,
        uint SubscriptionLevelCents,
        DateTime ExpiresOnUTC
    );
}
