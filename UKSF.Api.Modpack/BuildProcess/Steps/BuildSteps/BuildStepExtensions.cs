using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepExtensions : FileBuildStep
{
    public const string Name = "Extensions";
    private IBuildProcessTracker _processTracker;

    protected override Task SetupExecute()
    {
        _processTracker = ServiceProvider?.GetService<IBuildProcessTracker>();

        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        var uksfPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf", "intercept");
        var interceptPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@intercept");
        DirectoryInfo uksf = new(uksfPath);
        DirectoryInfo intercept = new(interceptPath);

        StepLogger.LogSurround("\nSigning extensions...");
        var files = GetDirectoryContents(uksf, "*.dll").Concat(GetDirectoryContents(intercept, "*.dll")).ToList();
        await SignExtensions(files);
        StepLogger.LogSurround("Signed extensions");
    }

    private async Task SignExtensions(IReadOnlyCollection<FileInfo> files)
    {
        var certPath = Path.Join(VariablesService.GetVariable("BUILD_PATH_CERTS").AsString(), "UKSFCert.pfx");
        var signTool = VariablesService.GetVariable("BUILD_PATH_SIGNTOOL").AsString();
        var signed = 0;
        var total = files.Count;
        await BatchProcessFiles(
            files,
            2,
            file =>
            {
                using BuildProcessHelper processHelper = new(
                    StepLogger,
                    Logger,
                    CancellationTokenSource,
                    true,
                    false,
                    true,
                    processTracker: _processTracker,
                    buildId: Build?.Id
                );
                processHelper.Run(file.DirectoryName, signTool, $"sign /f \"{certPath}\" \"{file.FullName}\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
                Interlocked.Increment(ref signed);
                return Task.CompletedTask;
            },
            () => $"Signed {signed} of {total} extensions",
            "Failed to sign extension"
        );
    }
}
