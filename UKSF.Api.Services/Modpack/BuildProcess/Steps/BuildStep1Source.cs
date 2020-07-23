using System;
using System.Threading.Tasks;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep1Source : BuildStep {
        public const string NAME = "Pull source";

        protected override async Task SetupExecute() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfihjugdfhjodfgh");
        }

        protected override async Task ProcessExecute() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfi4y5hu654hhjugdfhjodfgh");
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfihjtyfghrtytrtryfghfghfghugdfhjodfgh");
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", "4665uhftfghtfghfhgfgh");
        }

        protected override async Task TeardownExecute() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "echo", "C:/", "gdfiunjhdgfihjtyfghfghfghfghugdfhjodfgh");
        }
    }
}
