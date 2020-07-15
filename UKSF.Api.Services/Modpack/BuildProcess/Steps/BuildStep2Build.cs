using System;
using System.Threading.Tasks;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep2Build : BuildStep {
        public const string NAME = "Build";

        public override async Task Setup() {
            await base.Setup();
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", "y45 4y5 dgfh");
        }

        public override async Task Process() {
            await base.Process();
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            for (int i = 0; i < 100; i++) {
                await TaskUtilities.Delay(TimeSpan.FromMilliseconds(50), CancellationTokenSource.Token);
                await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", Guid.NewGuid().ToString());
            }
        }

        public override async Task Teardown() {
            await base.Teardown();
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", "hfg");
        }
    }
}
