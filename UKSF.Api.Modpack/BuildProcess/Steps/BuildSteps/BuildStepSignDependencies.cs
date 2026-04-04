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

        var result = CheckDependenciesNeedSigning(addons);
        if (result is null)
        {
            StepLogger.Log("No PBO files found in dependencies, skipping");
            return false;
        }

        if (result != "")
        {
            StepLogger.Log(result);
            return true;
        }

        var pboCount = addons.GetFiles("*.pbo", SearchOption.AllDirectories).Length;
        StepLogger.Log($"All {pboCount} dependencies PBOs have valid signatures, skipping");
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

    private static string CheckDependenciesNeedSigning(DirectoryInfo addons)
    {
        var pboFiles = addons.GetFiles("*.pbo", SearchOption.AllDirectories);
        if (pboFiles.Length == 0)
        {
            return null;
        }

        foreach (var pbo in pboFiles)
        {
            var bisigns = addons.GetFiles($"{pbo.Name}.*.bisign", SearchOption.AllDirectories);
            if (bisigns.Length == 0)
            {
                return $"Found PBOs without signatures ({pbo.Name}), signing required";
            }

            if (bisigns.Length > 1)
            {
                return $"Found PBOs with multiple signatures ({pbo.Name}), signing required";
            }

            if (bisigns[0].LastWriteTimeUtc < pbo.LastWriteTimeUtc)
            {
                return $"Found PBOs newer than their signatures ({pbo.Name}), signing required";
            }
        }

        return "";
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
