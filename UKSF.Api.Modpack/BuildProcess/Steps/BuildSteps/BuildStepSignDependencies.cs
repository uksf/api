using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepSignDependencies : FileBuildStep
{
    public const string Name = "Signatures";
    private int _batchSize = 10;
    private string _dsCreateKey;
    private string _dsSignFile;
    private string _keyName;
    private readonly int _signProcessTimeout = (int)TimeSpan.FromSeconds(20).TotalMilliseconds;

    public override bool CheckGuards()
    {
        StepLogger.LogSurround("\nChecking dependencies signatures...");
        var shouldSign = ShouldSignDependencies();
        StepLogger.LogSurround("Checked dependencies signatures");
        return shouldSign;
    }

    private bool ShouldSignDependencies()
    {
        var addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
        DirectoryInfo addons = new(addonsPath);
        if (!addons.Exists)
        {
            StepLogger.Log("Dependencies addons directory not found, signing required");
            return true;
        }

        if (Build.Environment == GameEnvironment.Rc && Build.BuildNumber == 1)
        {
            StepLogger.Log($"First RC build for {Build.Version}, signing all dependencies");
            return true;
        }

        var pboFiles = addons.GetFiles("*.pbo", SearchOption.AllDirectories);
        if (pboFiles.Length == 0)
        {
            StepLogger.Log("No PBO files found in dependencies, skipping");
            return false;
        }

        var issues = FindSigningIssues(addons, pboFiles);
        if (issues.Count > 0)
        {
            foreach (var issue in issues)
            {
                StepLogger.Log(issue);
            }

            StepLogger.Log("Signing required");
            return true;
        }

        StepLogger.Log($"All {pboFiles.Length} dependencies PBOs have valid signatures, skipping");
        return false;
    }

    protected override async Task SetupExecute()
    {
        _dsSignFile = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSSignFile.exe");
        _dsCreateKey = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSCreateKey.exe");
        _batchSize = VariablesService.GetVariable("BUILD_SIGNATURES_BATCH_SIZE").AsInt();
        _keyName = GetKeyname();

        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        var keysPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "keys");
        DirectoryInfo keygen = new(keygenPath);
        DirectoryInfo keys = new(keysPath);
        keygen.Create();
        keys.Create();

        StepLogger.LogSurround("\nClearing keys directories...");
        await DeleteDirectoryContents(keysPath);
        await DeleteDirectoryContents(keygenPath);
        StepLogger.LogSurround("Cleared keys directories");

        StepLogger.LogSurround("\nCreating key...");
        await RunProcess(keygenPath, _dsCreateKey, _keyName, _signProcessTimeout, false, true);
        StepLogger.Log($"Created {_keyName}");
        await CopyFiles(keygen, keys, [new FileInfo(Path.Join(keygenPath, $"{_keyName}.bikey"))]);
        StepLogger.LogSurround("Created key");
    }

    protected override async Task ProcessExecute()
    {
        var addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        DirectoryInfo addons = new(addonsPath);

        StepLogger.LogSurround("\nDeleting dependencies signatures...");
        await DeleteFiles(GetDirectoryContents(addons, "*.bisign*"));
        StepLogger.LogSurround("Deleted dependencies signatures");

        var repoFiles = GetDirectoryContents(addons, "*.pbo");
        StepLogger.LogSurround("\nSigning dependencies...");
        await SignFiles(keygenPath, addonsPath, repoFiles);
        StepLogger.LogSurround("Signed dependencies");
    }

    internal string GetKeyname()
    {
        return Build.Environment switch
        {
            GameEnvironment.Rc          => $"uksf_dependencies_{Build.Version}",
            GameEnvironment.Development => "uksf_dependencies_dev",
            _                           => throw new ArgumentException("Invalid build environment for dependency signing")
        };
    }

    private static List<string> FindSigningIssues(DirectoryInfo addons, FileInfo[] pboFiles)
    {
        List<string> issues = [];
        foreach (var pbo in pboFiles)
        {
            var bisigns = addons.GetFiles($"{pbo.Name}.*.bisign", SearchOption.AllDirectories);
            if (bisigns.Length == 0)
            {
                issues.Add($"{pbo.Name} has no signature");
            }
            else if (bisigns.Length > 1)
            {
                issues.Add($"{pbo.Name} has {bisigns.Length} signatures");
            }
            else if (bisigns[0].LastWriteTimeUtc < pbo.LastWriteTimeUtc)
            {
                issues.Add($"{pbo.Name} is newer than its signature");
            }
        }

        return issues;
    }

    private Task SignFiles(string keygenPath, string addonsPath, List<FileInfo> files)
    {
        var privateKey = Path.Join(keygenPath, $"{_keyName}.biprivatekey");
        var signed = 0;
        var total = files.Count;

        return BatchProcessFiles(
            files,
            _batchSize,
            async file =>
            {
                await RunProcess(addonsPath, _dsSignFile, $"\"{privateKey}\" \"{file.FullName}\"", _signProcessTimeout, false, true);
                Interlocked.Increment(ref signed);
            },
            () => $"Signed {signed} of {total} files",
            "Failed to sign file"
        );
    }
}
