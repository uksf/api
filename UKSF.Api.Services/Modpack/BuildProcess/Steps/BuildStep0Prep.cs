using System;
using System.Threading.Tasks;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep0Prep : BuildStep {
        public const string NAME = "Prep";

        public override async Task Setup() {
            await base.Setup();
            await Logger.Log("Nothing to do");
        }

        public override async Task Process() {
            await base.Process();
            await TaskUtilities.Delay(TimeSpan.FromSeconds(2), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "subst", "C:/", "P:", "\"D:/Arma/Arma 3 Projects\"");
            await TaskUtilities.Delay(TimeSpan.FromSeconds(2), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "subst", "C:/");
        }

        public override async Task Teardown() {
            await base.Teardown();
            await Logger.Log("Nothing to do");
        }
    }
}
