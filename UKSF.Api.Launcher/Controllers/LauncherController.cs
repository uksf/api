using System.Collections.Generic;
using System.Threading.Tasks;
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
    [Route("[controller]"), Permissions(Permissions.CONFIRMED, Permissions.MEMBER)]
    public class LauncherController : ControllerBase
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
        public void GetUpdate(string platform, string version) { }

        [HttpGet("version")]
        public string GetVersion()
        {
            return _variablesContext.GetSingle("LAUNCHER_VERSION").AsString();
        }

        [HttpPost("version"), Permissions(Permissions.ADMIN)]
        public async Task UpdateVersion([FromBody] JObject body)
        {
            var version = body["version"].ToString();
            await _variablesContext.Update("LAUNCHER_VERSION", version);
            await _launcherFileService.UpdateAllVersions();
            await _launcherHub.Clients.All.ReceiveLauncherVersion(version);
        }

        [HttpGet("download/setup")]
        public FileStreamResult GetLauncher()
        {
            return _launcherFileService.GetLauncherFile("UKSF Launcher Setup.msi");
        }

        [HttpGet("download/updater")]
        public FileStreamResult GetUpdater()
        {
            return _launcherFileService.GetLauncherFile("Updater", "UKSF.Launcher.Updater.exe");
        }

        [HttpPost("download/update")]
        public async Task<FileStreamResult> GetUpdatedFiles([FromBody] JObject body)
        {
            var files = JsonConvert.DeserializeObject<List<LauncherFile>>(body["files"].ToString());
            var updatedFiles = await _launcherFileService.GetUpdatedFiles(files);
            FileStreamResult stream = new(updatedFiles, "application/octet-stream");
            return stream;
        }

        [HttpPost("error")]
        public void ReportError([FromBody] JObject body)
        {
            var version = body["version"].ToString();
            var message = body["message"].ToString();
            // logger.Log(new LauncherLog(version, message) { userId = httpContextService.GetUserId(), name = displayNameService.GetDisplayName(accountService.GetUserAccount()) });
        }
    }
}
