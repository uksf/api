using System;
using System.Linq;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps
{
    public class GitBuildStep : BuildStep
    {
        internal string GitCommand(string workingDirectory, string command)
        {
            var results = new BuildProcessHelper(StepLogger, CancellationTokenSource, false, false, true).Run(
                workingDirectory,
                "cmd.exe",
                $"/c \"{command}\"",
                (int) TimeSpan.FromSeconds(10).TotalMilliseconds
            );
            return results.Count > 0 ? results.Last() : string.Empty;
        }
    }
}
