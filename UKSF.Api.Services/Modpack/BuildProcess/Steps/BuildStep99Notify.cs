using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep99Notify : BuildStep {
        public const string NAME = "Notify";
        private IDiscordService discordService;
        private IReleaseService releaseService;

        public override async Task Setup() {
            await base.Setup();
            discordService = ServiceWrapper.Provider.GetService<IDiscordService>();
            releaseService = ServiceWrapper.Provider.GetService<IReleaseService>();
            await Logger.Log("Retrieved services");
        }

        public override async Task Process() {
            await base.Process();

            if (Build.isRelease) {
                ModpackRelease release = releaseService.GetRelease(Build.version);
                await discordService.SendMessageToEveryone(
                    VariablesWrapper.VariablesDataService().GetSingle("DID_C_MODPACK_RELEASE").AsUlong(),
                    $"Modpack Update - {release.version}\nChangelog: <https://uk-sf.co.uk/modpack/releases?version={release.version}>\n\n{release.description}"
                );
            } else {
                await discordService.SendMessage(VariablesWrapper.VariablesDataService().GetSingle("DID_C_MODPACK_DEV").AsUlong(), GetDiscordMessage());
            }

            await Logger.Log("Notifications sent");
        }

        private string GetBuildMessage() =>
            Build.isReleaseCandidate
                ? $"New dev build available ({Build.buildNumber}) on the dev repository"
                : $"New release candidate ({Build.buildNumber}) available for {Build.version} on the stage repository";

        private string GetBuildLink() =>
            Build.isReleaseCandidate ? $"https://uk-sf.co.uk/modpack/builds-rc?version={Build.version}&build={Build.id}" : $"https://uk-sf.co.uk/modpack/builds-dev?build={Build.id}";

        private string GetDiscordMessage() =>
            $"Modpack {(Build.isReleaseCandidate ? "RC" : "Dev")} Build - {(Build.isReleaseCandidate ? $"{Build.version} RC# {Build.buildNumber}" : $"#{Build.buildNumber}")}\n{GetBuildMessage()}\n<{GetBuildLink()}>";
    }
}
