namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class GitBuildStep : BuildStep
{
    internal async Task<string> GitCommand(string workingDirectory, string command)
    {
        try
        {
            var timeoutMs = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;

            // Add git configurations to prevent hangs and credential prompts
            const string GitConfig = "git -c core.askpass='' -c credential.helper='' -c core.longpaths=true";
            var fullCommand = command.Replace("git", GitConfig);

            var results = await RunProcessModern(workingDirectory, "cmd.exe", $"/c \"{fullCommand}\"", timeoutMs, false, false, false, true);
            return results.Count > 0 ? results.Last() : string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions to preserve cancellation behavior
            throw;
        }
        catch (Exception ex)
        {
            StepLogger.LogWarning($"Git command failed: {command}. Error: {ex.Message}");
            throw new Exception($"Git operation failed: {command}", ex);
        }
    }
}
