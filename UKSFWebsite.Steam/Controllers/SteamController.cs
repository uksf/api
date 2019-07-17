using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Steam.Controllers {
    [Route("[controller]")]
    public class SteamController : Controller {
        private readonly string url;
        private readonly string urlReturn;

        private readonly IConfirmationCodeService confirmationCodeService;

        public SteamController(IConfirmationCodeService confirmationCodeService, IHostingEnvironment currentEnvironment) {
            this.confirmationCodeService = confirmationCodeService;

            url = currentEnvironment.IsDevelopment() ? "http://localhost:5100" : "https://steam.uk-sf.co.uk";
            urlReturn = currentEnvironment.IsDevelopment() ? "http://localhost:4200" : "https://uk-sf.co.uk";
        }

        [HttpGet]
        public IActionResult Get() => Challenge(new AuthenticationProperties {RedirectUri = $"{url}/steam/success"}, "Steam");

        [HttpGet("application")]
        public IActionResult GetFromApplication() => Challenge(new AuthenticationProperties {RedirectUri = $"{url}/steam/success/application"}, "Steam");

        [HttpGet("success")]
        public async Task<IActionResult> Success() => Redirect($"{urlReturn}/profile?{await GetUrlParameters()}");

        [HttpGet("success/application")]
        public async Task<IActionResult> SuccessFromApplication() => Redirect($"{urlReturn}/application?{await GetUrlParameters()}");

        private async Task<string> GetUrlParameters() {
            string[] idParts = HttpContext.User.Claims.First(claim => claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value.Split('/');
            string id = idParts[idParts.Length - 1];
            string code = await confirmationCodeService.CreateConfirmationCode(id, true);
            return $"validation={code}&steamid={id}";
        }
    }
}
