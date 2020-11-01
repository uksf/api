using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class DisplayNameController : Controller {
        private readonly IDisplayNameService displayNameService;

        public DisplayNameController(IDisplayNameService displayNameService) => this.displayNameService = displayNameService;

        [HttpGet("{id}")]
        public IActionResult GetName(string id) => Ok(new {name = displayNameService.GetDisplayName(id)});
    }
}
