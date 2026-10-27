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

    internal bool FullSign { get; private set; }
    private List<FileInfo> _affectedPbos = [];

    public override bool CheckGuards()
    {
        StepLogger.LogSurround("\nChecking dependencies signatures...");
        var shouldSign = DetermineSignMode();
        StepLogger.LogSurround("Checked dependencies signatures");
        return shouldSign;
    }

    protected override async Task SetupExecute()
    {
        _dsSignFile = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSSignFile.exe");
        _dsCreateKey = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSCreateKey.exe");
        _batchSize = VariablesService.GetVariable("BUILD_SIGNATURES_BATCH_SIZE").AsInt();
        _keyName = GetKeyname();

        if (FullSign)
        {
            await SetupFullSign();
        }
        else
        {
            SetupPartialSign();
        }
    }

    protected override async Task ProcessExecute()
    {
        if (FullSign)
        {
            await ProcessFullSign();
        }
        else
        {
            await ProcessPartialSign();
        }
    }

    private bool DetermineSignMode()
    {
        var addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
        DirectoryInfo addons = new(addonsPath);
        if (!addons.Exists)
        {
            StepLogger.Log("Dependencies addons directory not found");
            FullSign = true;
            return true;
        }

        if (Build.Environment == GameEnvironment.Rc && Build.BuildNumber == 1)
        {
            StepLogger.Log($"First RC build for {Build.Version}");
            FullSign = true;
            return true;
        }

        var pboFiles = DependencyPbos();
        if (pboFiles.Count == 0)
        {
            StepLogger.Log("No PBO files found");
            return false;
        }

        List<FileInfo> changedPbos = [];
        List<FileInfo> missingSignaturePbos = [];

        foreach (var pbo in pboFiles)
        {
            var bisigns = Bisigns(pbo);
            if (bisigns.Count != 1)
            {
                missingSignaturePbos.Add(pbo);
            }
            else if (bisigns[0].LastWriteTimeUtc < pbo.LastWriteTimeUtc)
            {
                changedPbos.Add(pbo);
            }
        }

        if (changedPbos.Count == 0 && missingSignaturePbos.Count == 0)
        {
            StepLogger.Log($"All {pboFiles.Count} PBOs have valid signatures");
            return false;
        }

        foreach (var pbo in changedPbos)
        {
            StepLogger.Log($"{pbo.Name} is newer than its signature");
        }

        foreach (var pbo in missingSignaturePbos)
        {
            StepLogger.Log($"{pbo.Name} is missing a valid signature");
        }

        if (changedPbos.Count > 0)
        {
            FullSign = true;
            return true;
        }

        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        var existingKey = Directory.Exists(keygenPath) ? Directory.GetFiles(keygenPath, "*.biprivatekey").FirstOrDefault() : null;

        if (existingKey is null)
        {
            FullSign = true;
            return true;
        }

        _affectedPbos = missingSignaturePbos;
        return true;
    }

    private async Task SetupFullSign()
    {
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

    private void SetupPartialSign()
    {
        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        var privateKeyPath = Directory.GetFiles(keygenPath, "*.biprivatekey").First();

        StepLogger.LogSurround("\nLoading key...");
        StepLogger.Log($"Loaded {Path.GetFileNameWithoutExtension(privateKeyPath)}");
        StepLogger.LogSurround("Loaded key");
    }

    private async Task ProcessFullSign()
    {
        var addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        DirectoryInfo addons = new(addonsPath);
        var repoFiles = DependencyPbos();

        StepLogger.LogSurround("\nDeleting dependencies signatures...");
        await DeleteFiles(GetDirectoryContents(addons, "*.bisign*"));
        await DeleteFiles(repoFiles.Where(x => x.DirectoryName != addons.FullName).SelectMany(PatchedPboSignatureFiles).ToList());
        StepLogger.LogSurround("Deleted dependencies signatures");

        StepLogger.LogSurround("\nSigning dependencies...");
        await SignFiles(keygenPath, repoFiles);
        StepLogger.LogSurround("Signed dependencies");
    }

    private async Task ProcessPartialSign()
    {
        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");

        StepLogger.LogSurround("\nCleaning affected signatures...");
        foreach (var pbo in _affectedPbos)
        {
            await DeleteFiles(Bisigns(pbo));
        }

        StepLogger.LogSurround("Cleaned affected signatures");

        StepLogger.LogSurround($"\nSigning {_affectedPbos.Count} affected PBOs...");
        await SignFiles(keygenPath, _affectedPbos);
        StepLogger.LogSurround($"Signed {_affectedPbos.Count} affected PBOs");
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

    internal List<FileInfo> DependencyPbos()
    {
        var repoPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
        var dependencies = new DirectoryInfo(Path.Join(repoPath, BuildStepPatchDependencies.DependenciesMod, "addons"));
        var pbos = dependencies.Exists ? dependencies.GetFiles("*.pbo", SearchOption.AllDirectories).ToList() : [];

        var patcherState = new DirectoryInfo(Path.Join(GetBuildEnvironmentPath(), "PatcherState"));
        var patchedMods = patcherState.Exists ? patcherState.GetDirectories("@*").Select(x => x.Name) : [];
        foreach (var mod in patchedMods.Where(x => !x.Equals(BuildStepPatchDependencies.DependenciesMod, StringComparison.OrdinalIgnoreCase)))
        {
            var addons = new DirectoryInfo(Path.Join(repoPath, mod, "addons"));
            if (addons.Exists)
            {
                pbos.AddRange(addons.GetFiles("*.pbo").Where(pbo => Bisigns(pbo).Count == PatchedPboSignatureFiles(pbo).Count));
            }
        }

        return pbos;
    }

    private static List<FileInfo> Bisigns(FileInfo pbo)
    {
        return pbo.Directory!.GetFiles($"{pbo.Name}.*.bisign").ToList();
    }

    private static List<FileInfo> PatchedPboSignatureFiles(FileInfo pbo)
    {
        return pbo.Directory!.GetFiles($"{pbo.Name}.uksf_dependencies_*.bisign*").ToList();
    }

    private Task SignFiles(string keygenPath, List<FileInfo> files)
    {
        var privateKey = Path.Join(keygenPath, $"{_keyName}.biprivatekey");
        var signed = 0;
        var total = files.Count;

        return BatchProcessFiles(
            files,
            _batchSize,
            async file =>
            {
                await RunProcess(Path.GetDirectoryName(file.FullName), _dsSignFile, $"\"{privateKey}\" \"{file.FullName}\"", _signProcessTimeout, false, true);
                Interlocked.Increment(ref signed);
            },
            () => $"Signed {signed} of {total} files",
            "Failed to sign file"
        );
    }
}
