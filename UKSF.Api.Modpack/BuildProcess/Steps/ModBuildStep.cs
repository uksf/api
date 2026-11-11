namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class ModBuildStep : FileBuildStep
{
    internal bool IsBuildNeeded(string key)
    {
        if (!GetEnvironmentVariable<bool>($"{key}_updated"))
        {
            StepLogger.Log("\nBuild is not needed");
            return false;
        }

        return true;
    }

    internal const string Hemtt = "hemtt";

    internal static string HemttCommand(string arguments = "")
    {
        return $"{arguments} -v";
    }

    internal Task RunHemttRelease(string rootPath, TimeSpan timeout)
    {
        return RunProcess(rootPath, Hemtt, HemttCommand("release --no-archive"), (int)timeout.TotalMilliseconds, true, redirectStderrToOutput: true);
    }
}
