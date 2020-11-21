using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class DisplayNameController : Controller {
        private readonly IDisplayNameService _displayNameService;

        public DisplayNameController(IDisplayNameService displayNameService) => _displayNameService = displayNameService;

        [HttpGet("{id}")]
        public IActionResult GetName(string id) => Ok(new { name = _displayNameService.GetDisplayName(id) });
    }
}
