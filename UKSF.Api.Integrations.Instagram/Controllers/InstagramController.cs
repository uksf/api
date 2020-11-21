using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Integrations.Instagram.Models;
using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram.Controllers {
    [Route("[controller]")]
    public class InstagramController : Controller {
        private readonly IInstagramService _instagramService;

        public InstagramController(IInstagramService instagramService) => _instagramService = instagramService;

        [HttpGet]
        public IEnumerable<InstagramImage> GetImages() => _instagramService.GetImages();
    }
}
