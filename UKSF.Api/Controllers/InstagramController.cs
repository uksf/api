using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Models.Integrations;

namespace UKSF.Api.Controllers {
    [Route("[controller]")]
    public class InstagramController : Controller {
        private readonly IInstagramService instagramService;

        public InstagramController(IInstagramService instagramService) => this.instagramService = instagramService;

        [HttpGet]
        public IEnumerable<InstagramImage> GetImages() => instagramService.GetImages();
    }
}
