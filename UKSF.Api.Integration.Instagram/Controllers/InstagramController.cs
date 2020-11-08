using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Integration.Instagram.Models;
using UKSF.Api.Integration.Instagram.Services;

namespace UKSF.Api.Integration.Instagram.Controllers {
    [Route("[controller]")]
    public class InstagramController : Controller {
        private readonly IInstagramService instagramService;

        public InstagramController(IInstagramService instagramService) => this.instagramService = instagramService;

        [HttpGet]
        public IEnumerable<InstagramImage> GetImages() => instagramService.GetImages();
    }
}
