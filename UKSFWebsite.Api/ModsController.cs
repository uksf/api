using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api {
    [Route("[controller]"), Authorize, Roles(RoleDefinitions.CONFIRMED, RoleDefinitions.MEMBER)]
    public class ModsController : Controller {

        // TODO: Return size of modpack folder
        [HttpGet("size")]
        public IActionResult Index() => Ok("37580963840");
    }
}
