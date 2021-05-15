using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Launcher.Models;
using UKSF.Api.Launcher.Services;
using UKSF.Api.Launcher.Signalr.Clients;
using UKSF.Api.Launcher.Signalr.Hubs;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;

// ReSharper disable UnusedVariable
// ReSharper disable UnusedParameter.Global
// ReSharper disable NotAccessedField.Local

namespace UKSF.Api.Launcher.Controllers
{
    [Route("[controller]"), Authorize, Permissions(Permissions.CONFIRMED, Permissions.MEMBER)]
    public class LauncherController : Controller
    {
        private readonly IDisplayNameService _displayNameService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILauncherFileService _launcherFileService;
        private readonly IHubContext<LauncherHub, ILauncherClient> _launcherHub;
        private readonly ILauncherService _launcherService;
        private readonly IVariablesContext _variablesContext;
        private readonly IVariablesService _variablesService;

        public LauncherController(
            IVariablesContext variablesContext,
            IHubContext<LauncherHub, ILauncherClient> launcherHub,
            ILauncherService launcherService,
            ILauncherFileService launcherFileService,
            IHttpContextService httpContextService,
            IDisplayNameService displayNameService,
            IVariablesService variablesService
        )
        {
            _variablesContext = variablesContext;
            _launcherHub = launcherHub;
            _launcherService = launcherService;
            _launcherFileService = launcherFileService;
            _httpContextService = httpContextService;
            _displayNameService = displayNameService;
            _variablesService = variablesService;
        }

        [HttpGet("update/{platform}/{version}")]
        public IActionResult GetUpdate(string platform, string version)
        {
            return Ok();
        }

        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            return Ok(_variablesContext.GetSingle("LAUNCHER_VERSION").AsString());
        }

        [HttpPost("version"), Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> UpdateVersion([FromBody] JObject body)
        {
            string version = body["version"].ToString();
            await _variablesContext.Update("LAUNCHER_VERSION", version);
            await _launcherFileService.UpdateAllVersions();
            await _launcherHub.Clients.All.ReceiveLauncherVersion(version);
            return Ok();
        }

        [HttpGet("download/setup")]
        public IActionResult GetLauncher()
        {
            return _launcherFileService.GetLauncherFile("UKSF Launcher Setup.msi");
        }

        [HttpGet("download/updater")]
        public IActionResult GetUpdater()
        {
            return _launcherFileService.GetLauncherFile("Updater", "UKSF.Launcher.Updater.exe");
        }

        [HttpPost("download/update")]
        public async Task<IActionResult> GetUpdatedFiles([FromBody] JObject body)
        {
            List<LauncherFile> files = JsonConvert.DeserializeObject<List<LauncherFile>>(body["files"].ToString());
            Stream updatedFiles = await _launcherFileService.GetUpdatedFiles(files);
            FileStreamResult stream = new(updatedFiles, "application/octet-stream");
            return stream;
        }

        [HttpPost("error")]
        public IActionResult ReportError([FromBody] JObject body)
        {
            string version = body["version"].ToString();
            string message = body["message"].ToString();
            // logger.Log(new LauncherLog(version, message) { userId = httpContextService.GetUserId(), name = displayNameService.GetDisplayName(accountService.GetUserAccount()) });

            return Ok();
        }
    }
}
