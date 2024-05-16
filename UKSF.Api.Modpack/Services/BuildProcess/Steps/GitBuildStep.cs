namespace UKSF.Api.Modpack.Services.BuildProcess.Steps;

public class GitBuildStep : BuildStep
{
    internal string GitCommand(string workingDirectory, string command)
    {
        var processHelper = new BuildProcessHelper(StepLogger, Logger, CancellationTokenSource, false, false, true);
        var results = processHelper.Run(workingDirectory, "cmd.exe", $"/c \"{command}\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        return results.Count > 0 ? results.Last() : string.Empty;
    }
}
