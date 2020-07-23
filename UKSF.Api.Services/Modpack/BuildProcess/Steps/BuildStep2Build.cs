using System;
using System.Threading.Tasks;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep2Build : BuildStep {
        public const string NAME = "Build";

        protected override async Task SetupExecute() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", "ibguyredsrgiuhbi7dh54t");
        }

        protected override async Task ProcessExecute() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            for (int i = 0; i < 100; i++) {
                await TaskUtilities.Delay(TimeSpan.FromMilliseconds(25), CancellationTokenSource.Token);
                await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", Guid.NewGuid().ToString());
            }
        }

        protected override async Task TeardownExecute() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", "hfg");
        }
    }
}
