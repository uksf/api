using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.BuildProcess.Steps.Common;

[BuildStep(Name)]
public class BuildStepDeploy : FileBuildStep
{
    public const string Name = "Deploy";

    protected override async Task ProcessExecute()
    {
        string sourcePath;
        string targetPath;
        if (Build.Environment == GameEnvironment.Release)
        {
            StepLogger.Log("Deploying files from RC to release");
            sourcePath = Path.Join(GetEnvironmentPath(GameEnvironment.Rc), "Repo");
            targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
        }
        else
        {
            StepLogger.Log("Deploying files from build to repo");
            sourcePath = Path.Join(GetBuildEnvironmentPath(), "Build");
            targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
        }

        StepLogger.LogSurround("\nAdding new files...");
        await AddFiles(sourcePath, targetPath);
        StepLogger.LogSurround("Added new files");

        StepLogger.LogSurround("\nCopying updated files...");
        await UpdateFiles(sourcePath, targetPath);
        StepLogger.LogSurround("Copied updated files");

        StepLogger.LogSurround("\nDeleting removed files...");
        await DeleteFiles(sourcePath, targetPath, Build.Environment != GameEnvironment.Release);
        StepLogger.LogSurround("Deleted removed files");

        if (Build.Environment != GameEnvironment.Rc)
        {
            StepLogger.LogSurround("\nRemoving RC optional...");
            await RemoveRcOptional(targetPath);
            StepLogger.LogSurround("Removed RC optional");
        }

        StepLogger.LogSurround("\nRemoving UKSF optionals...");
        await RemoveUksfOptionalsFolder(targetPath);
        StepLogger.LogSurround("Removed UKSF optionals");
    }

    private Task RemoveRcOptional(string repoPath)
    {
        var addonsPath = Path.Join(repoPath, "@uksf", "addons");
        return DeleteFiles(GetDirectoryContents(new DirectoryInfo(addonsPath), "uksf_rc.*"));
    }

    private Task RemoveUksfOptionalsFolder(string repoPath)
    {
        var buildPath = Path.Join(repoPath, "@uksf");
        return DeleteDirectoryContents(Path.Join(buildPath, "optionals"));
    }
}
