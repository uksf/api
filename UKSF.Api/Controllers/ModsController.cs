using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Authorize, Permissions(Permissions.CONFIRMED, Permissions.MEMBER)]
    public class ModsController : Controller {
        // TODO: Return size of modpack folder
        [HttpGet("size")]
        public IActionResult Index() => Ok("37580963840");
    }
}