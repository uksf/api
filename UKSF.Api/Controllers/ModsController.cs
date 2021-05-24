using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Shared;

namespace UKSF.Api.Controllers
{
    [Route("[controller]"), Authorize, Permissions(Permissions.CONFIRMED, Permissions.MEMBER)]
    public class ModsController : Controller
    {
        // TODO: Return size of modpack folder
        [HttpGet("size")]
        public string Index()
        {
            return "37580963840";
        }
    }
}
