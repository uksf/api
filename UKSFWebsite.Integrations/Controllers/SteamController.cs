using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using UKSFWebsite.Api.Interfaces.Utility;

namespace UKSFWebsite.Integrations.Controllers {
    [Route("[controller]")]
    public class SteamController : Controller {
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly string url;
        private readonly string urlReturn;

        public SteamController(IConfirmationCodeService confirmationCodeService, IHostEnvironment currentEnvironment) {
            this.confirmationCodeService = confirmationCodeService;

            url = currentEnvironment.IsDevelopment() ? "http://localhost:5100" : "https://integrations.uk-sf.co.uk";
            urlReturn = currentEnvironment.IsDevelopment() ? "http://localhost:4200" : "https://uk-sf.co.uk";
        }

        [HttpGet]
        public IActionResult Get() => Challenge(new AuthenticationProperties {RedirectUri = $"{url}/integrations/success"}, "Steam");

        [HttpGet("application")]
        public IActionResult GetFromApplication() => Challenge(new AuthenticationProperties {RedirectUri = $"{url}/integrations/success/application"}, "Steam");

        [HttpGet("success")]
        public async Task<IActionResult> Success() => Redirect($"{urlReturn}/profile?{await GetUrlParameters()}");

        [HttpGet("success/application")]
        public async Task<IActionResult> SuccessFromApplication() => Redirect($"{urlReturn}/application?{await GetUrlParameters()}");

        private async Task<string> GetUrlParameters() {
            string[] idParts = HttpContext.User.Claims.First(claim => claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value.Split('/');
            string id = idParts[^1];
            string code = await confirmationCodeService.CreateConfirmationCode(id, true);
            return $"validation={code}&steamid={id}";
        }
    }
}
