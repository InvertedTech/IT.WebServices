using System;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Data
{
    public interface IResetTokenDataProvider
    {
        Task SaveToken(Guid userId, byte[] tokenHash, DateTime expiresOnUTC);
        Task<ResetTokenRecord?> GetByTokenHash(byte[] tokenHash);
        Task DeleteToken(Guid userId);
    }

    public class ResetTokenRecord
    {
        public Guid UserID { get; set; }
        public byte[] TokenHash { get; set; } = Array.Empty<byte>();
        public DateTime ExpiresOnUTC { get; set; }
        public DateTime CreatedOnUTC { get; set; }
    }
}
