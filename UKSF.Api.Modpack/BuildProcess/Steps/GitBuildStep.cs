namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class GitBuildStep : BuildStep
{
    protected override Task SetupExecute()
    {
        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    internal string GitCommand(string workingDirectory, string command)
    {
        var timeoutMinutes = 2; // Increased from 10 seconds to 2 minutes for git operations
        var timeoutMs = (int)TimeSpan.FromMinutes(timeoutMinutes).TotalMilliseconds;

        // Add git configurations to prevent hangs and credential prompts
        var gitConfigPrefix = "git -c core.askpass='' -c credential.helper='' -c core.longpaths=true";
        var fullCommand = $"{gitConfigPrefix} {command}";

        StepLogger.Log($"Executing git command: {command} (timeout: {timeoutMinutes} minutes)");
        var results = RunProcess(workingDirectory, "cmd.exe", $"/c \"{fullCommand}\"", timeoutMs, false, false, false, true);
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
