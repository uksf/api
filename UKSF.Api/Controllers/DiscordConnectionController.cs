using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class DiscordConnectionController : ControllerBase
{
    private readonly string _botToken;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IUksfLogger _logger;
    private readonly string _url;
    private readonly string _urlReturn;
    private readonly IVariablesService _variablesService;

    public DiscordConnectionController(
        IConfirmationCodeService confirmationCodeService,
        IVariablesService variablesService,
        IUksfLogger logger,
        IOptions<AppSettings> options
    )
    {
        _confirmationCodeService = confirmationCodeService;
        _variablesService = variablesService;
        _logger = logger;

        var appSettings = options.Value;
        _clientId = appSettings.Secrets.Discord.ClientId;
        _clientSecret = appSettings.Secrets.Discord.ClientSecret;
        _botToken = appSettings.Secrets.Discord.BotToken;
        _url = appSettings.RedirectApiUrl;
        _urlReturn = appSettings.RedirectUrl;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Redirect(
            $"https://discord.com/api/oauth2/authorize?client_id={_clientId}&redirect_uri={HttpUtility.UrlEncode($"{_url}/discordconnection/success")}&response_type=code&scope=identify%20guilds.join"
        );
    }

    [HttpGet("application")]
    public IActionResult GetFromApplication()
    {
        return Redirect(
            $"https://discord.com/api/oauth2/authorize?client_id={_clientId}&redirect_uri={HttpUtility.UrlEncode($"{_url}/discordconnection/success/application")}&response_type=code&scope=identify%20guilds.join"
        );
    }

    [HttpGet("success")]
    public async Task<IActionResult> Success([FromQuery] string code)
    {
        return Redirect($"{_urlReturn}/profile?{await GetUrlParameters(code, $"{_url}/discordconnection/success")}");
    }

    [HttpGet("success/application")]
    public async Task<IActionResult> SuccessFromApplication([FromQuery] string code)
    {
        return Redirect($"{_urlReturn}/application?{await GetUrlParameters(code, $"{_url}/discordconnection/success/application")}");
    }

    private async Task<string> GetUrlParameters(string code, string redirectUrl)
    {
        using HttpClient client = new();
        var response = await client.PostAsync(
            "https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    { "client_id", _clientId },
                    { "client_secret", _clientSecret },
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", redirectUrl },
                    { "scope", "identify guilds.join" }
                }
            )
        );
        var result = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning($"A discord connection request was denied by the user, or an error occurred: {result}");
            return "discordid=fail";
        }

        var resultJson = JsonNode.Parse(result);
        var token = resultJson.GetValueFromObject("access_token");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("A discord connection request failed. Could not get access token");
            return "discordid=fail";
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var userResponse = await client.GetAsync("https://discord.com/api/users/@me");
        var userResult = await userResponse.Content.ReadAsStringAsync();
        var userResultJson = JsonNode.Parse(userResult);
        var id = userResultJson.GetValueFromObject("id");
        var username = userResultJson.GetValueFromObject("username");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(username))
        {
            _logger.LogWarning($"A discord connection request failed. Could not get username ({username}) or id ({id}) or an error occurred: {userResult}");
            return "discordid=fail";
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", _botToken);
        var addToServerResponse = await client.PutAsync(
            $"https://discord.com/api/guilds/{_variablesService.GetVariable("DID_SERVER").AsUlong()}/members/{id}",
            new StringContent($"{{\"access_token\":\"{token}\"}}", Encoding.UTF8, "application/json")
        );
        var added = "true";
        if (!addToServerResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                $"Failed to add '{username}' to guild: {addToServerResponse.StatusCode}, {addToServerResponse.Content.ReadAsStringAsync().Result}"
            );
            added = "false";
        }

        var confirmationCode = await _confirmationCodeService.CreateConfirmationCode(id);
        return $"validation={confirmationCode}&discordid={id}&added={added}";
    }
}
