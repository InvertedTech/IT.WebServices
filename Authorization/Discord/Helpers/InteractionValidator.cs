using NSec.Cryptography;
using System.Text;

namespace IT.WebServices.Authorization.Discord.Helpers
{
    public class InteractionValidator
    {
        public bool IsValid(string rawBody, string timestamp, string signatureHeader, string discordPublicKey)
        {
            CheckArgs(rawBody, timestamp, signatureHeader, discordPublicKey);

            PublicKey.TryImport(
                    SignatureAlgorithm.Ed25519,
                    Convert.FromHexString(discordPublicKey),
                    KeyBlobFormat.RawPublicKey,
                    out var pk
                );

            if (pk is null) throw new IOException("discordPublicKey is either null or not hex string");

            var message = Encoding.UTF8.GetBytes(
                timestamp + rawBody);
            var signature = Convert.FromHexString(signatureHeader);

            return SignatureAlgorithm.Ed25519.Verify(pk, message, signature);
        }

        private void CheckArgs(string rawBody, string timestamp, string signatureHeader, string discordPublicKey)
        {
            if (string.IsNullOrEmpty(rawBody)) throw new ArgumentNullException("Raw Body Is Empty");
            if (string.IsNullOrEmpty(timestamp)) throw new ArgumentNullException("Timestamp Is Empty");
            if (string.IsNullOrEmpty(signatureHeader)) throw new ArgumentNullException("Signature Header Is Empty");
            if (string.IsNullOrEmpty(discordPublicKey)) throw new ArgumentNullException("Discord Public Key Is Empty");
        }
    }
}
