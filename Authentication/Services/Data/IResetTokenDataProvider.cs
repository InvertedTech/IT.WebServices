using System;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Data
{
    public interface IResetTokenDataProvider
    {
        Task SaveToken(Guid userId, string tokenHash, DateTime expiresOnUTC);
        Task<ResetTokenRecord?> GetByTokenHash(string tokenHash);
        Task DeleteToken(Guid userId);
    }

    public class ResetTokenRecord
    {
        public Guid UserID { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresOnUTC { get; set; }
        public DateTime CreatedOnUTC { get; set; }
    }
}
