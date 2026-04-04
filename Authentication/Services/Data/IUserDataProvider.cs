using  IT.WebServices.Fragments.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Data
{
    public interface IUserDataProvider
    {
        Task<bool> ChangeEmailIndex(string email, Guid id);
        Task<bool> ChangeLoginIndex(string oldLoginName, string newLoginName, Guid id);
        Task<bool> Create(UserRecord user);
        Task<bool> Delete(Guid userId);
        Task<bool> Exists(Guid userId);
        Task<bool> EmailExists(string email);
        Task<bool> LoginExists(string loginName);
        IAsyncEnumerable<UserRecord> GetAll();
        Task<Guid[]> GetAllIds();
        Task<UserRecord?> GetById(Guid userId);
        Task<UserRecord?> GetByEmail(string email);
        Task<UserRecord?> GetByLogin(string loginName);
        Task<UserRecord?> GetByOldUserID(string oldUserId);
        Task<Guid> GetIdByMicrosoftAuthProviderUserId(string userId);
        Task Save(UserRecord user);
    }
}
