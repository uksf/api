﻿using System;
using System.Threading.Tasks;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common {
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
            switch (Build.environment) {
                case GameEnvironment.RELEASE: {
                    ModpackRelease release = releaseService.GetRelease(Build.version);
                    await discordService.SendMessageToEveryone(VariablesService.GetVariable("DID_C_MODPACK_RELEASE").AsUlong(), GetDiscordMessage(release));
                    break;
                }
                case GameEnvironment.RC:
                    await discordService.SendMessage(VariablesService.GetVariable("DID_C_MODPACK_DEV").AsUlong(), GetDiscordMessage());
                    break;
                case GameEnvironment.DEV: break;
                default: throw new ArgumentOutOfRangeException();
            }

            Logger.Log("Notifications sent");
        }

        private string GetBuildMessage() => $"New release candidate available for {Build.version} on the rc repository";

        private string GetBuildLink() => $"https://uk-sf.co.uk/modpack/builds-rc?version={Build.version}&build={Build.id}";

        private string GetDiscordMessage(ModpackRelease release = null) =>
            release == null
                ? $"Modpack RC Build - {Build.version} RC# {Build.buildNumber}\n{GetBuildMessage()}\n<{GetBuildLink()}>"
                : $"Modpack Update - {release.version}\nFull Changelog: <https://uk-sf.co.uk/modpack/releases?version={release.version}>\n\nSummary:\n```{release.description}```";
    }
}