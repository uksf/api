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
using UKSF.Api.Base.Events;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class DiscordConnectionController : Controller {
        private readonly string botToken;
        private readonly string clientId;
        private readonly string clientSecret;

        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly IVariablesService variablesService;
        private readonly ILogger logger;
        private readonly string url;
        private readonly string urlReturn;

        public DiscordConnectionController(IConfirmationCodeService confirmationCodeService, IConfiguration configuration, IHostEnvironment currentEnvironment, IVariablesService variablesService, ILogger logger) {
            this.confirmationCodeService = confirmationCodeService;
            this.variablesService = variablesService;
            this.logger = logger;
            clientId = configuration.GetSection("Discord")["clientId"];
            clientSecret = configuration.GetSection("Discord")["clientSecret"];
            botToken = configuration.GetSection("Discord")["botToken"];

            url = currentEnvironment.IsDevelopment() ? "http://localhost:5000" : "https://api.uk-sf.co.uk";
            urlReturn = currentEnvironment.IsDevelopment() ? "http://localhost:4200" : "https://uk-sf.co.uk";
        }

        [HttpGet]
        public IActionResult Get() =>
            Redirect(
                $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={HttpUtility.UrlEncode($"{url}/discordconnection/success")}&response_type=code&scope=identify%20guilds.join"
            );

        [HttpGet("application")]
        public IActionResult GetFromApplication() =>
            Redirect(
                $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={HttpUtility.UrlEncode($"{url}/discordconnection/success/application")}&response_type=code&scope=identify%20guilds.join"
            );

        [HttpGet("success")]
        public async Task<IActionResult> Success([FromQuery] string code) => Redirect($"{urlReturn}/profile?{await GetUrlParameters(code, $"{url}/discordconnection/success")}");

        [HttpGet("success/application")]
        public async Task<IActionResult> SuccessFromApplication([FromQuery] string code) =>
            Redirect($"{urlReturn}/application?{await GetUrlParameters(code, $"{url}/discordconnection/success/application")}");

        private async Task<string> GetUrlParameters(string code, string redirectUrl) {
            using HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.PostAsync(
                "https://discord.com/api/oauth2/token",
                new FormUrlEncodedContent(
                    new Dictionary<string, string> {
                        { "client_id", clientId },
                        { "client_secret", clientSecret },
                        { "grant_type", "authorization_code" },
                        { "code", code },
                        { "redirect_uri", redirectUrl },
                        { "scope", "identify guilds.join" }
                    }
                )
            );
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                logger.LogWarning($"A discord connection request was denied by the user, or an error occurred: {result}");
                return "discordid=fail";
            }

            string token = JObject.Parse(result)["access_token"]?.ToString();
            if (string.IsNullOrEmpty(token)) {
                logger.LogWarning("A discord connection request failed. Could not get access token");
                return "discordid=fail";
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await client.GetAsync("https://discord.com/api/users/@me");
            result = await response.Content.ReadAsStringAsync();
            string id = JObject.Parse(result)["id"]?.ToString();
            string username = JObject.Parse(result)["username"]?.ToString();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(username)) {
                logger.LogWarning($"A discord connection request failed. Could not get username ({username}) or id ({id}) or an error occurred: {result}");
                return "discordid=fail";
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);
            response = await client.PutAsync(
                $"https://discord.com/api/guilds/{variablesService.GetVariable("DID_SERVER").AsUlong()}/members/{id}",
                new StringContent($"{{\"access_token\":\"{token}\"}}", Encoding.UTF8, "application/json")
            );
            string added = "true";
            if (!response.IsSuccessStatusCode) {
                logger.LogWarning($"Failed to add '{username}' to guild: {response.StatusCode}, {response.Content.ReadAsStringAsync().Result}");
                added = "false";
            }

            string confirmationCode = await confirmationCodeService.CreateConfirmationCode(id);
            return $"validation={confirmationCode}&discordid={id}&added={added}";
        }
    }
}
