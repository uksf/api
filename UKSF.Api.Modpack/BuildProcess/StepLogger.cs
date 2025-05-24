using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.BuildProcess;

public interface IStepLogger
{
    void LogStart();
    void LogSuccess();
    void LogCancelled();
    void LogSkipped();
    void LogWarning(string message);
    void LogError(Exception exception);
    void LogSurround(string log);
    void Log(string log, string colour = "");
    void LogInline(string log);
}

public class StepLogger(ModpackBuildStep buildStep) : IStepLogger
{
    public void LogStart()
    {
        LogLines($"Starting: {buildStep.Name}", string.Empty);
    }

    public void LogSuccess()
    {
        LogLines(
            $"\nFinished{(buildStep.BuildResult == ModpackBuildResult.Warning ? " with warning" : "")}: {buildStep.Name}",
            buildStep.BuildResult == ModpackBuildResult.Warning ? "orangered" : "green"
        );
    }

    public void LogCancelled()
    {
        LogLines("\nBuild cancelled", "goldenrod");
    }

    public void LogSkipped()
    {
        LogLines($"\nSkipped: {buildStep.Name}", "gray");
    }

    public void LogWarning(string message)
    {
        LogLines($"Warning\n{message}", "orangered");
    }

    public void LogError(Exception exception)
    {
        LogLines($"Error\n{exception.Message}\n{exception.StackTrace}\n\nFailed: {buildStep.Name}", "red");
    }

    public void LogSurround(string log)
    {
        LogLines(log, "cadetblue");
    }

    public void Log(string log, string colour = "")
    {
        LogLines(log, colour);
    }

    public void LogInline(string log)
    {
        PushLogUpdate(new List<ModpackBuildStepLogItem> { new() { Text = log } }, true);
    }

    private void LogLines(string log, string colour = "")
    {
        var logs = log.Split("\n").Select(x => new ModpackBuildStepLogItem { Text = x, Colour = string.IsNullOrEmpty(x) ? "" : colour }).ToList();
        if (logs.Count == 0)
        {
            return;
        }

        PushLogUpdate(logs);
    }

    private void PushLogUpdate(IEnumerable<ModpackBuildStepLogItem> logs, bool inline = false)
    {
        var logList = logs.ToList();
        if (logList.Count == 0)
        {
            return;
        }

        if (inline)
        {
            if (buildStep.Logs.Count > 0)
            {
                buildStep.Logs[^1] = logList.First();
            }
            else
            {
                buildStep.Logs.AddRange(logList);
            }
        }
        else
        {
            buildStep.Logs.AddRange(logList);
        }
    }
}
