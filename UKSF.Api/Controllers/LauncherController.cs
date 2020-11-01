using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Base;
using UKSF.Api.Base.Services;
using UKSF.Api.Interfaces.Admin;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Launcher;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Launcher;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Signalr.Hubs.Integrations;
using UKSF.Common;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Authorize, Permissions(Permissions.CONFIRMED, Permissions.MEMBER)]
    public class LauncherController : Controller {
        private readonly IDisplayNameService displayNameService;
        private readonly ILauncherFileService launcherFileService;
        private readonly IHttpContextService httpContextService;
        private readonly IHubContext<LauncherHub, ILauncherClient> launcherHub;
        private readonly ILauncherService launcherService;

        private readonly IVariablesDataService variablesDataService;
        private readonly IVariablesService variablesService;

        public LauncherController(
            IVariablesDataService variablesDataService,
            IHubContext<LauncherHub, ILauncherClient> launcherHub,
            ILauncherService launcherService,
            ILauncherFileService launcherFileService,
            IHttpContextService httpContextService,
            IDisplayNameService displayNameService,
            IVariablesService variablesService
        ) {
            this.variablesDataService = variablesDataService;
            this.launcherHub = launcherHub;
            this.launcherService = launcherService;
            this.launcherFileService = launcherFileService;
            this.httpContextService = httpContextService;

            this.displayNameService = displayNameService;
            this.variablesService = variablesService;
        }

        [HttpGet("update/{platform}/{version}")]
        public IActionResult GetUpdate(string platform, string version) => Ok();

        [HttpGet("version")]
        public IActionResult GetVersion() => Ok(variablesDataService.GetSingle("LAUNCHER_VERSION").AsString());

        [HttpPost("version"), Permissions(Permissions.ADMIN)]
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
            // logger.Log(new LauncherLogMessage(version, message) { userId = httpContextService.GetUserId(), name = displayNameService.GetDisplayName(accountService.GetUserAccount()) });

            return Ok();
        }
    }
}
