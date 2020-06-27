using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.MEMBER)]
    public class InstagramController : Controller {
        private readonly IInstagramService instagramService;

        public InstagramController(IInstagramService instagramService) => this.instagramService = instagramService;

        [HttpGet, Authorize]
        public IActionResult GetImages() => Ok(instagramService.GetImages());
    }
}
