using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Launcher;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Launcher;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Signalr.Hubs.Integrations;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Authorize, Roles(RoleDefinitions.CONFIRMED, RoleDefinitions.MEMBER)]
    public class LauncherController : Controller {
        private readonly IDisplayNameService displayNameService;
        private readonly ILauncherFileService launcherFileService;
        private readonly IHubContext<LauncherHub, ILauncherClient> launcherHub;
        private readonly ILauncherService launcherService;
        private readonly ISessionService sessionService;
        private readonly IVariablesDataService variablesDataService;

        public LauncherController(IVariablesDataService variablesDataService, IHubContext<LauncherHub, ILauncherClient> launcherHub, ILauncherService launcherService, ILauncherFileService launcherFileService, ISessionService sessionService, IDisplayNameService displayNameService) {
            this.variablesDataService = variablesDataService;
            this.launcherHub = launcherHub;
            this.launcherService = launcherService;
            this.launcherFileService = launcherFileService;
            this.sessionService = sessionService;
            this.displayNameService = displayNameService;
        }

        [HttpGet("update/{platform}/{version}")]
        public IActionResult GetUpdate(string platform, string version) => Ok();

        [HttpGet("version")]
        public IActionResult GetVersion() => Ok(variablesDataService.GetSingle("LAUNCHER_VERSION").AsString());

        [HttpPost("version"), Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> UpdateVersion([FromBody] JObject body) {
            string version = body["version"].ToString();
            await variablesDataService.Update("LAUNCHER_VERSION", version);
            await launcherFileService.UpdateAllVersions();
            await launcherHub.Clients.All.ReceiveLauncherVersion(version);
            return Ok();
        }

        [HttpGet("download/setup")]
        public IActionResult GetLauncher() => launcherFileService.GetLauncherFile("UKSF Launcher Setup.msi");

        [HttpGet("download/updater")]
        public IActionResult GetUpdater() => launcherFileService.GetLauncherFile("Updater", "UKSF.Launcher.Updater.exe");

        [HttpPost("download/update")]
        public async Task<IActionResult> GetUpdatedFiles([FromBody] JObject body) {
            List<LauncherFile> files = JsonConvert.DeserializeObject<List<LauncherFile>>(body["files"].ToString());
            Stream updatedFiles = await launcherFileService.GetUpdatedFiles(files);
            FileStreamResult stream = new FileStreamResult(updatedFiles, "application/octet-stream");
            return stream;
        }

        [HttpPost("error")]
        public IActionResult ReportError([FromBody] JObject body) {
            string version = body["version"].ToString();
            string message = body["message"].ToString();
            LogWrapper.Log(new LauncherLogMessage(version, message) {userId = sessionService.GetContextId(), name = displayNameService.GetDisplayName(sessionService.GetContextAccount())});

            return Ok();
        }
    }
}
