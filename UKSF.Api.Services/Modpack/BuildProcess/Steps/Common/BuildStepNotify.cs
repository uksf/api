using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepNotify : BuildStep {
        public const string NAME = "Notify";
        private IDiscordService discordService;
        private IReleaseService releaseService;

        protected override Task SetupExecute() {
            discordService = ServiceWrapper.Provider.GetService<IDiscordService>();
            releaseService = ServiceWrapper.Provider.GetService<IReleaseService>();
            Logger.Log("Retrieved services");
            return Task.CompletedTask;
        }

        protected override async Task ProcessExecute() {
            if (Build.environment == GameEnvironment.RELEASE) {
                ModpackRelease release = releaseService.GetRelease(Build.version);
                await discordService.SendMessageToEveryone(VariablesWrapper.VariablesDataService().GetSingle("DID_C_MODPACK_RELEASE").AsUlong(), GetDiscordMessage(release));
            } else {
                await discordService.SendMessage(VariablesWrapper.VariablesDataService().GetSingle("DID_C_MODPACK_RELEASE").AsUlong(), GetDiscordMessage()); // TODO: Change back to DID_C_MODPACK_DEV and change DID_C_MODPACK_RELEASE to live channel
            }

            Logger.Log("Notifications sent");
        }

        private string GetBuildMessage() =>
            Build.environment == GameEnvironment.RC
                ? $"New dev build available ({Build.buildNumber}) on the dev repository"
                : $"New release candidate ({Build.buildNumber}) available for {Build.version} on the rc repository";

        private string GetBuildLink() =>
            Build.environment == GameEnvironment.RC ? $"https://uk-sf.co.uk/modpack/builds-rc?version={Build.version}&build={Build.id}" : $"https://uk-sf.co.uk/modpack/builds-dev?build={Build.id}";

        private string GetDiscordMessage(ModpackRelease release = null) =>
            release == null
                ? $"Modpack {(Build.environment == GameEnvironment.RC ? "RC" : "Dev")} Build - {(Build.environment == GameEnvironment.RC ? $"{Build.version} RC# {Build.buildNumber}" : $"#{Build.buildNumber}")}\n{GetBuildMessage()}\n<{GetBuildLink()}>"
                : $"Modpack Update - {release.version}\nChangelog: <https://uk-sf.co.uk/modpack/releases?version={release.version}>\n\n{release.description}";
    }
}
