using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("[controller]")]
    public class SteamConnectionController : ControllerBase
    {
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly string _url;
        private readonly string _urlReturn;

        public SteamConnectionController(IConfirmationCodeService confirmationCodeService, IHostEnvironment currentEnvironment)
        {
            _confirmationCodeService = confirmationCodeService;

            _url = currentEnvironment.IsDevelopment() ? "http://localhost:5000" : "https://api.uk-sf.co.uk";
            _urlReturn = currentEnvironment.IsDevelopment() ? "http://localhost:4200" : "https://uk-sf.co.uk";
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Challenge(new AuthenticationProperties { RedirectUri = $"{_url}/steamconnection/success" }, "Steam");
        }

        [HttpGet("application")]
        public IActionResult GetFromApplication()
        {
            return Challenge(new AuthenticationProperties { RedirectUri = $"{_url}/steamconnection/success/application" }, "Steam");
        }

        [HttpGet("success")]
        public async Task<IActionResult> Success([FromQuery] string id)
        {
            return Redirect($"{_urlReturn}/profile?{await GetUrlParameters(id)}");
        }

        [HttpGet("success/application")]
        public async Task<IActionResult> SuccessFromApplication([FromQuery] string id)
        {
            return Redirect($"{_urlReturn}/application?{await GetUrlParameters(id)}");
        }

        private async Task<string> GetUrlParameters(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "steamid=fail";
            }

            var code = await _confirmationCodeService.CreateConfirmationCode(id);
            return $"validation={code}&steamid={id}";
        }
    }
}
