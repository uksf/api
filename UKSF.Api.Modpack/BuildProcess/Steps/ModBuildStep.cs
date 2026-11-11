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
}
