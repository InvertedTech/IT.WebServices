using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IT.WebServices.Fragments.Settings;

namespace IT.WebServices.Clients.Settings
{
    public class CategoryHelper
    {
        private readonly PublicSettingsClient settingsClient;

        public CategoryHelper(PublicSettingsClient settingsClient)
        {
            this.settingsClient = settingsClient;
        }

        public CategoryRecord[] GetAll()
        {
            return settingsClient.PublicData.Result.CMS?.Categories?.ToArray() ?? [];
        }

        public CategoryRecord? GetCategoryById(string id)
        {
            return settingsClient.PublicData.Result.CMS?.Categories?.FirstOrDefault(c => c.CategoryId == id);
        }

        public CategoryRecord? GetCategoryBySlug(string slug)
        {
            return settingsClient.PublicData.Result.CMS?.Categories?.FirstOrDefault(c => c.UrlStub == slug);
        }
    }
}
