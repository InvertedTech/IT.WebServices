using IT.WebServices.Fragments.Careers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Careers.Data
{
    public interface ICareersDataProvider
    {
        Task<CareerRecord> Save(CareerRecord record);
        Task<bool> Exists(Guid entryId);
        Task<CareerRecord> Get(Guid entryId);
        IAsyncEnumerable<CareerRecord> GetAll();
    }
}
