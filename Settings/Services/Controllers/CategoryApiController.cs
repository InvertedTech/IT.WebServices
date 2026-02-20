using Microsoft.AspNetCore.Mvc;
using System;
using IT.WebServices.Fragments.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using IT.WebServices.Authentication;
using IT.WebServices.Settings.Services.Data;
using IT.WebServices.Settings.Services.Models;
using System.Linq;

namespace IT.WebServices.Settings.Services.Controllers
{
    [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
    [Route("/api/settings/category")]
    [ApiController]
    public class CategoryApiController : Controller
    {
        private readonly ILogger logger;
        private readonly ISettingsDataProvider dataProvider;

        public CategoryApiController(ILogger<CategoryApiController> logger, ISettingsDataProvider dataProvider)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create(CreateCategoryModel model)
        {
            if (!model.IsValid())
                return NotFound();

            var rec = await dataProvider.Get();

            var cat = model.ToRecord();
            rec.Public.CMS.Categories.Add(cat);

            await dataProvider.Save(rec);

            return Ok(cat);
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var rec = await dataProvider.Get();

            var cat = rec.Public.CMS.Categories.FirstOrDefault(c => c.CategoryId == id);
            if (cat == null)
                return NotFound();

            rec.Public.CMS.Categories.Remove(cat);

            await dataProvider.Save(rec);

            return Ok();
        }
    }
}
