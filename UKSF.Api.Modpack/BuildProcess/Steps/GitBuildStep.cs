namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class GitBuildStep : BuildStep
{
    private IBuildProcessTracker _processTracker;

    protected override Task SetupExecute()
    {
        _processTracker = ServiceProvider?.GetService<IBuildProcessTracker>();

        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    internal string GitCommand(string workingDirectory, string command)
    {
        using var processHelper = new BuildProcessHelper(
            StepLogger,
            Logger,
            CancellationTokenSource,
            false,
            false,
            true,
            processTracker: _processTracker,
            buildId: Build?.Id
        );
        var timeoutMinutes = 2; // Increased from 10 seconds to 2 minutes for git operations
        var timeoutMs = (int)TimeSpan.FromMinutes(timeoutMinutes).TotalMilliseconds;

        // Add git configurations to prevent hangs and credential prompts
        var gitConfigPrefix = "git -c core.askpass='' -c credential.helper='' -c core.longpaths=true";
        var fullCommand = $"{gitConfigPrefix} {command}";

        StepLogger.Log($"Executing git command: {command} (timeout: {timeoutMinutes} minutes)");
        var results = processHelper.Run(workingDirectory, "cmd.exe", $"/c \"{fullCommand}\"", timeoutMs);
        return results.Count > 0 ? results.Last() : string.Empty;
    }

    internal string SafeGitCommand(string workingDirectory, string command, int timeoutMinutes = 2)
    {
        try
        {
            return GitCommand(workingDirectory, command);
        }
        catch (Exception ex)
        {
            StepLogger.LogWarning($"Git command failed: {command}. Error: {ex.Message}");
            throw new Exception($"Git operation failed: {command}", ex);
        }
    }
}
