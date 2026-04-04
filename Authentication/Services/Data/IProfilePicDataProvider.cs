using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Data
{
    public interface IProfilePicDataProvider
    {
        Task<bool> Delete(Guid userId);
        Task<byte[]?> GetById(Guid userId);
        Task Save(Guid userId, byte[] bytes);
    }
}
