using System;
using System.Threading.Tasks;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep0Prep : BuildStep {
        public const string NAME = "Prep";

        protected override async Task ProcessExecute() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(2), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "subst", "C:/", "P:", "\"D:/Arma/Arma 3 Projects\"");
            await TaskUtilities.Delay(TimeSpan.FromSeconds(2), CancellationTokenSource.Token);
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, "subst", "C:/");
        }
    }
}
