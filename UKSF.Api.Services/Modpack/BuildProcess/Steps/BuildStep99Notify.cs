using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep99Notify : BuildStep {
        public const string NAME = "Notify";
        private IAccountService accountService;
        private IDiscordService discordService;
        private INotificationsService notificationsService;
        private IReleaseService releaseService;
        private IUnitsService unitsService;

        public override async Task Setup() {
            await base.Setup();
            unitsService = ServiceWrapper.Provider.GetService<IUnitsService>();
            accountService = ServiceWrapper.Provider.GetService<IAccountService>();
            notificationsService = ServiceWrapper.Provider.GetService<INotificationsService>();
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
                string message = GetBuildMessage();
                string link = GetBuildLink();

                string[] missionsId = VariablesWrapper.VariablesDataService().GetSingle("ROLE_ID_MISSIONS").AsArray();
                string testersId = VariablesWrapper.VariablesDataService().GetSingle("ROLE_ID_TESTERS").AsString();
                List<string> missionMakers = unitsService.Data.GetSingle(x => missionsId.Contains(x.id)).members;
                List<string> developers = unitsService.Data.GetSingle(testersId).members;
                List<Account> accounts = missionMakers.Union(developers).Select(x => accountService.Data.GetSingle(x)).Where(x => x.settings.notificationsBuilds).ToList();
                foreach (Notification notification in accounts.Select(account => new Notification { owner = account.id, icon = NotificationIcons.BUILD, message = message, link = link })) {
                    notificationsService.Add(notification);
                }

                await discordService.SendMessageToEveryone(VariablesWrapper.VariablesDataService().GetSingle("DID_C_MODPACK_DEV").AsUlong(), GetDiscordMessage());
            }

            await Logger.Log("Notifications sent");
        }

        private string GetBuildMessage() =>
            Build.isReleaseCandidate
                ? $"New dev build available ({Build.buildNumber}) on the dev repository"
                : $"New release candidate ({Build.buildNumber}) available for {Build.version} on the stage repository";

        private string GetBuildLink() => Build.isReleaseCandidate ? $"modpack/builds-rc?version={Build.version}&build={Build.id}" : $"modpack/builds-dev?build={Build.id}";

        private string GetDiscordMessage() =>
            $"Modpack {(Build.isReleaseCandidate ? "RC" : "Dev")} Build - {(Build.isReleaseCandidate ? $"{Build.version} RC# {Build.buildNumber}" : $"#{Build.buildNumber}")}";
    }
}
