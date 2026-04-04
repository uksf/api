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
            StepLogger.Log("Dependencies addons directory not found, full sign required");
            FullSign = true;
            return true;
        }

        if (Build.Environment == GameEnvironment.Rc && Build.BuildNumber == 1)
        {
            StepLogger.Log($"First RC build for {Build.Version}, full sign required");
            FullSign = true;
            return true;
        }

        var pboFiles = addons.GetFiles("*.pbo", SearchOption.AllDirectories);
        if (pboFiles.Length == 0)
        {
            StepLogger.Log("No PBO files found in dependencies, skipping");
            return false;
        }

        var bisignsByPbo = addons.GetFiles("*.bisign", SearchOption.AllDirectories)
                                 .GroupBy(b => ExtractPboName(b.Name))
                                 .ToDictionary(g => g.Key, g => g.ToArray());

        List<FileInfo> changedPbos = [];
        List<FileInfo> missingSignaturePbos = [];

        foreach (var pbo in pboFiles)
        {
            if (!bisignsByPbo.TryGetValue(pbo.Name, out var bisigns) || bisigns.Length != 1)
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
            StepLogger.Log($"All {pboFiles.Length} dependencies PBOs have valid signatures, skipping");
            return false;
        }

        foreach (var pbo in changedPbos)
        {
            StepLogger.Log($"{pbo.Name} is newer than its signature");
        }

        foreach (var pbo in missingSignaturePbos)
        {
            StepLogger.Log($"{pbo.Name} has invalid signatures");
        }

        if (changedPbos.Count > 0)
        {
            StepLogger.Log("PBO content changed, full sign required");
            FullSign = true;
            return true;
        }

        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        var existingKey = Directory.Exists(keygenPath) ? Directory.GetFiles(keygenPath, "*.biprivatekey").FirstOrDefault() : null;

        if (existingKey is null)
        {
            StepLogger.Log("No existing private key found, full sign required");
            FullSign = true;
            return true;
        }

        StepLogger.Log($"Reusing existing key, partial sign for {missingSignaturePbos.Count} PBOs");
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

        StepLogger.Log($"\nUsing existing key: {Path.GetFileNameWithoutExtension(privateKeyPath)}");
    }

    private async Task ProcessFullSign()
    {
        var addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        DirectoryInfo addons = new(addonsPath);

        StepLogger.LogSurround("\nDeleting dependencies signatures...");
        await DeleteFiles(GetDirectoryContents(addons, "*.bisign*"));
        StepLogger.LogSurround("Deleted dependencies signatures");

        var repoFiles = GetDirectoryContents(addons, "*.pbo");
        StepLogger.LogSurround("\nSigning dependencies...");
        await SignFiles(keygenPath, repoFiles);
        StepLogger.LogSurround("Signed dependencies");
    }

    private async Task ProcessPartialSign()
    {
        var addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
        var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
        DirectoryInfo addons = new(addonsPath);

        var allBisigns = addons.GetFiles("*.bisign", SearchOption.AllDirectories)
                               .GroupBy(b => ExtractPboName(b.Name))
                               .ToDictionary(g => g.Key, g => g.ToList());

        StepLogger.LogSurround("\nCleaning affected signatures...");
        foreach (var pbo in _affectedPbos)
        {
            if (allBisigns.TryGetValue(pbo.Name, out var staleBisigns))
            {
                await DeleteFiles(staleBisigns);
            }
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

    private static string ExtractPboName(string bisignFileName)
    {
        // Bisign format: {name}.pbo.{keyname}.bisign
        var pboIndex = bisignFileName.IndexOf(".pbo.", StringComparison.Ordinal);
        return pboIndex >= 0 ? bisignFileName[..(pboIndex + 4)] : bisignFileName;
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
