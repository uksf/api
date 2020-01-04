using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Services.Admin;
using UKSFWebsite.Api.Services.Message;

namespace UKSFWebsite.Integrations.Controllers {
    [Route("[controller]")]
    public class DiscordController : Controller {
        private readonly string botToken;
        private readonly string clientId;
        private readonly string clientSecret;

        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly string url;
        private readonly string urlReturn;

        public DiscordController(IConfirmationCodeService confirmationCodeService, IConfiguration configuration, IHostEnvironment currentEnvironment) {
            this.confirmationCodeService = confirmationCodeService;
            clientId = configuration.GetSection("Discord")["clientId"];
            clientSecret = configuration.GetSection("Discord")["clientSecret"];
            botToken = configuration.GetSection("Discord")["botToken"];

            url = currentEnvironment.IsDevelopment() ? "http://localhost:5100" : "https://integrations.uk-sf.co.uk";
            urlReturn = currentEnvironment.IsDevelopment() ? "http://localhost:4200" : "https://uk-sf.co.uk";
        }

        [HttpGet]
        public IActionResult Get() => Redirect($"https://discordapp.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={HttpUtility.UrlEncode($"{url}/discord/success")}&response_type=code&scope=identify%20guilds.join");

        [HttpGet("application")]
        public IActionResult GetFromApplication() => Redirect($"https://discordapp.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={HttpUtility.UrlEncode($"{url}/discord/success/application")}&response_type=code&scope=identify%20guilds.join");

        [HttpGet("success")]
        public async Task<IActionResult> Success([FromQuery] string code) => Redirect($"{urlReturn}/profile?{await GetUrlParameters(code, $"{url}/discord/success")}");

        [HttpGet("success/application")]
        public async Task<IActionResult> SuccessFromApplication([FromQuery] string code) => Redirect($"{urlReturn}/application?{await GetUrlParameters(code, $"{url}/discord/success/application")}");

        private async Task<string> GetUrlParameters(string code, string redirectUrl) {
            using HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.PostAsync(
                "https://discordapp.com/api/oauth2/token",
                new FormUrlEncodedContent(
                    new Dictionary<string, string> {
                        {"client_id", clientId},
                        {"client_secret", clientSecret},
                        {"grant_type", "authorization_code"},
                        {"code", code},
                        {"redirect_uri", redirectUrl},
                        {"scope", "identify guilds.join"}
                    }
                )
            );
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                LogWrapper.Log("A discord connection request was denied");
                return "discordid=fail";
            }
            string token = JObject.Parse(result)["access_token"].ToString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await client.GetAsync("https://discordapp.com/api/users/@me");
            string user = await response.Content.ReadAsStringAsync();
            string id = JObject.Parse(user)["id"].ToString();
            string username = JObject.Parse(user)["username"].ToString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);
            response = await client.PutAsync($"https://discordapp.com/api/guilds/{VariablesWrapper.VariablesDataService().GetSingle("DID_SERVER").AsUlong()}/members/{id}", new StringContent($"{{\"access_token\":\"{token}\"}}", Encoding.UTF8, "application/json"));
            string added = "true";
            if (!response.IsSuccessStatusCode) {
                LogWrapper.Log($"Failed to add '{username}' to guild: {response.StatusCode}, {response.Content.ReadAsStringAsync().Result}");
                added = "false";
            }

            string confirmationCode = await confirmationCodeService.CreateConfirmationCode(id, true);
            return $"validation={confirmationCode}&discordid={id}&added={added}";
        }
    }
}
