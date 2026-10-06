using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

// Applies the modpack's hash-pinned dependency patches (modpack/patches) to the PBOs in @uksf_dependencies with
// uksf-patcher, before the dependencies are signed. A patched PBO is newer than its signature, so the signing step
// re-signs it. An upstream change to a patched PBO fails the build until its manifest is re-pinned.
[BuildStep(Name)]
public class BuildStepPatchDependencies : FileBuildStep
{
    public const string Name = "Patch Dependencies";
    private readonly int _patcherTimeout = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
    private string _patcher;

    internal string PatchesPath => Path.Join(GetBuildSourcesPath(), "modpack", "patches");
    internal string AddonsPath => Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");

    // Outside Repo, so the pristine backups never ship. Kept between builds.
    internal string StatePath => Path.Join(GetBuildEnvironmentPath(), "PatcherState");

    public override bool CheckGuards()
    {
        var manifests = Directory.Exists(PatchesPath) ? Directory.GetFiles(PatchesPath, "*.json").Length : 0;
        // State without manifests means a patch was removed: apply restores the pristine PBO.
        var states = Directory.Exists(StatePath) ? Directory.GetFiles(StatePath, "*.state.json").Length : 0;
        if (manifests == 0 && states == 0)
        {
            StepLogger.Log("No dependency patches");
            return false;
        }

        StepLogger.Log($"{manifests} dependency patch manifests, {states} patched PBOs on record");
        return true;
    }

    protected override Task SetupExecute()
    {
        _patcher = Path.Join(VariablesService.GetVariable("BUILD_PATH_PATCHER").AsString(), "uksf-patcher.exe");
        if (!File.Exists(_patcher))
        {
            throw new Exception($"uksf-patcher not found at '{_patcher}' (variable BUILD_PATH_PATCHER)");
        }

        Directory.CreateDirectory(StatePath);
        Directory.CreateDirectory(PatchesPath);
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        var args = $"--addons \"{AddonsPath}\" --patches \"{PatchesPath}\" --state \"{StatePath}\"";

        StepLogger.LogSurround("\nApplying dependency patches...");
        var before = SnapshotPbos();
        await RunProcess(AddonsPath, _patcher, $"apply {args}", _patcherTimeout, true);
        InvalidateSignatures(before);
        StepLogger.LogSurround("Applied dependency patches");

        StepLogger.LogSurround("\nVerifying dependency patches...");
        await RunProcess(AddonsPath, _patcher, $"verify {args}", _patcherTimeout, true);
        StepLogger.LogSurround("Verified dependency patches");
    }

    internal Dictionary<string, (long Length, DateTime WriteTime)> SnapshotPbos()
    {
        return Directory.GetFiles(AddonsPath, "*.pbo").ToDictionary(x => x, x => (new FileInfo(x).Length, File.GetLastWriteTimeUtc(x)));
    }

    // The signing step finds changed PBOs by timestamp alone, so a signature dated after the patch would survive.
    // Deleting the signatures of every PBO the patcher wrote makes the signing step sign them.
    internal void InvalidateSignatures(Dictionary<string, (long Length, DateTime WriteTime)> before)
    {
        foreach (var (pbo, now) in SnapshotPbos())
        {
            if (before.TryGetValue(pbo, out var was) && was == now)
            {
                continue;
            }

            foreach (var bisign in Directory.GetFiles(AddonsPath, $"{Path.GetFileName(pbo)}.*.bisign"))
            {
                File.Delete(bisign);
                StepLogger.Log($"Removed {Path.GetFileName(bisign)}: {Path.GetFileName(pbo)} was patched");
            }
        }
    }
}
