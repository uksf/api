using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Interfaces.Utility;

namespace UKSF.Api.Controllers {
    [Route("[controller]")]
    public class SteamConnectionController : Controller {
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly string url;
        private readonly string urlReturn;

        public SteamConnectionController(IConfirmationCodeService confirmationCodeService, IHostEnvironment currentEnvironment) {
            this.confirmationCodeService = confirmationCodeService;

            url = currentEnvironment.IsDevelopment() ? "http://localhost:5000" : "https://api.uk-sf.co.uk";
            urlReturn = currentEnvironment.IsDevelopment() ? "http://localhost:4200" : "https://uk-sf.co.uk";
        }

        [HttpGet]
        public IActionResult Get() => Challenge(new AuthenticationProperties { RedirectUri = $"{url}/steamconnection/success" }, "Steam");

        [HttpGet("application")]
        public IActionResult GetFromApplication() => Challenge(new AuthenticationProperties { RedirectUri = $"{url}/steamconnection/success/application" }, "Steam");

        [HttpGet("success")]
        public async Task<IActionResult> Success([FromQuery] string id) => Redirect($"{urlReturn}/profile?{await GetUrlParameters(id)}");

        [HttpGet("success/application")]
        public async Task<IActionResult> SuccessFromApplication([FromQuery] string id) => Redirect($"{urlReturn}/application?{await GetUrlParameters(id)}");

        private async Task<string> GetUrlParameters(string id) {
            if (string.IsNullOrEmpty(id)) {
                return "steamid=fail";
            }

            string code = await confirmationCodeService.CreateConfirmationCode(id);
            return $"validation={code}&steamid={id}";
        }
    }
}
