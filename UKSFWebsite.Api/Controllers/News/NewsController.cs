using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace UKSFWebsite.Api.Controllers.News {
    [Route("[controller]")]
    public class NewsController : Controller {
        [HttpGet]
        public async Task<IActionResult> Get() {
            JArray output = new JArray();

            using (HttpClient client = new HttpClient()) {
                client.DefaultRequestHeaders.Add("Authorization", "Bot Mzc5ODQ2NDMwNTA1OTU5NDQ1.DO4raw.2fXCTl97ED-KYi0kUK6tXKHfcuw");
                string result = await (await client.GetAsync("https://discordapp.com/api/v6/channels/311547331935862786/messages")).Content.ReadAsStringAsync();

                JArray json = JArray.Parse(result);
                foreach (JToken jToken in json) {
                    JObject message = (JObject) jToken;
                    if (message.GetValue("content").ToString().StartsWith("@everyone")) {
                        string user = await (await client.GetAsync($"https://discordapp.com/api/v6/guilds/311543678126653451/members/{(message.GetValue("author") as JObject)?.GetValue("id")}")).Content.ReadAsStringAsync();
                        output.Add(JObject.FromObject(new {message = CleanNewsMessage(message.GetValue("content").ToString().Replace("@everyone", "")), author = JObject.Parse(user).GetValue("nick"), timestamp = message.GetValue("timestamp")}));
                    }
                }
            }

            return Ok(new {content = output});
        }

        private static string CleanNewsMessage(string source) {
            string[] filters = {" ", "-", "\n"};
            foreach (string filter in filters) {
                if (!source.StartsWith(filter)) continue;
                source = source.Remove(0, filter.ToCharArray().Length);
                source = CleanNewsMessage(source);
            }

            return source;
        }
    }
}
