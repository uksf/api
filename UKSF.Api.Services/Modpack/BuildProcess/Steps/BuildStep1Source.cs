using System;
using System.Threading.Tasks;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep1Source : BuildStep {
        public const string NAME = "Pull source";

        public override async Task Setup() {
            await base.Setup();
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfihjugdfhjodfgh");
        }

        public override async Task Process() {
            await base.Process();
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfi4y5hu654hhjugdfhjodfgh");
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfihjtyfghrtytrtryfghfghfghugdfhjodfgh");
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", "4665uhftfghtfghfhgfgh");
        }

        public override async Task Teardown() {
            await base.Teardown();
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfihjtyfghfghfghfghugdfhjodfgh");
        }
    }
}
