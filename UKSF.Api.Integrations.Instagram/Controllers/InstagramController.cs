using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Integrations.Instagram.Models;
using UKSF.Api.Integrations.Instagram.ScheduledActions;
using UKSF.Api.Integrations.Instagram.Services;
using UKSF.Api.Shared;

namespace UKSF.Api.Integrations.Instagram.Controllers
{
    [Route("[controller]")]
    public class InstagramController : Controller
    {
        private readonly IActionInstagramToken _actionInstagramToken;
        private readonly IInstagramService _instagramService;

        public InstagramController(IInstagramService instagramService, IActionInstagramToken actionInstagramToken)
        {
            _instagramService = instagramService;
            _actionInstagramToken = actionInstagramToken;
        }

        [HttpGet]
        public IEnumerable<InstagramImage> GetImages()
        {
            return _instagramService.GetImages();
        }

        [HttpGet("refreshToken"), Permissions(Permissions.ADMIN)]
        public async Task RefreshToken()
        {
            await _actionInstagramToken.Reset();
        }
    }
}
