using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepExtensions : FileBuildStep
{
    public const string Name = "Extensions";

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
            async file =>
            {
                await RunProcessModern(
                    file.DirectoryName,
                    signTool,
                    $"sign /f \"{certPath}\" \"{file.FullName}\"",
                    (int)TimeSpan.FromSeconds(10).TotalMilliseconds,
                    false,
                    true,
                    false,
                    true
                );
                Interlocked.Increment(ref signed);
            },
            () => $"Signed {signed} of {total} extensions",
            "Failed to sign extension"
        );
    }
}
