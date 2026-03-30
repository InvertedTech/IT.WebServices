using IT.WebServices.Fragments.Dashboard;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Dashboard.Services.Data
{
    public interface IUserDataProvider
    {
        Task<UserKpis> GetUserKpis();
    }
}
