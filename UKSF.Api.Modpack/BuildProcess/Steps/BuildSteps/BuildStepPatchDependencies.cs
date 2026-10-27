using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepPatchDependencies : FileBuildStep
{
    public const string Name = "Patches";
    public const string DependenciesMod = "@uksf_dependencies";
    private readonly int _patcherTimeout = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
    private string _patcher;

    internal string PatchesPath => Path.Join(GetBuildSourcesPath(), "modpack", "patches");
    internal string RepoPath => Path.Join(GetBuildEnvironmentPath(), "Repo");
    internal string StatePath => Path.Join(GetBuildEnvironmentPath(), "PatcherState");

    internal string AddonsPath(string mod) => Path.Join(RepoPath, mod, "addons");

    public override bool CheckGuards()
    {
        var mods = PatchedMods();
        var manifests = mods.Sum(mod => Files(Path.Join(PatchesPath, mod), "*.json"));
        var patchedPbosOnRecord = mods.Sum(mod => Files(Path.Join(StatePath, mod), "*.state.json")) + Files(StatePath, "*.state.json");
        if (manifests == 0 && patchedPbosOnRecord == 0 && Files(PatchesPath, "*.json") == 0)
        {
            StepLogger.Log("No dependency patches");
            return false;
        }

        StepLogger.Log($"{manifests} patch manifests for {string.Join(", ", mods)}, {patchedPbosOnRecord} patched PBOs on record");
        return true;
    }

    protected override Task SetupExecute()
    {
        _patcher = Path.Join(VariablesService.GetVariable("BUILD_PATH_PATCHER").AsString(), "uksf-patcher.exe");
        if (!File.Exists(_patcher))
        {
            throw new Exception($"uksf-patcher not found at '{_patcher}' (variable BUILD_PATH_PATCHER)");
        }

        if (Files(PatchesPath, "*.json") > 0)
        {
            throw new Exception($"Patch manifests must be in a mod folder such as '{Path.Join(PatchesPath, DependenciesMod)}', not in '{PatchesPath}'");
        }

        Directory.CreateDirectory(StatePath);
        MoveUngroupedStateToDependencies();
        StepLogger.Log($"Using {_patcher}");
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        foreach (var mod in PatchedMods())
        {
            var addons = AddonsPath(mod);
            if (!Directory.Exists(addons))
            {
                throw new Exception($"Patches target {mod}, but '{addons}' does not exist");
            }

            var patches = Path.Join(PatchesPath, mod);
            var state = Path.Join(StatePath, mod);
            Directory.CreateDirectory(patches);
            Directory.CreateDirectory(state);
            var args = $"--addons \"{addons}\" --patches \"{patches}\" --state \"{state}\"";

            StepLogger.LogSurround($"\nApplying {mod} patches...");
            var before = SnapshotPbos(addons);
            await RunProcess(addons, _patcher, $"apply {args}", _patcherTimeout, true);
            StepLogger.LogSurround($"Applied {mod} patches");

            StepLogger.LogSurround($"\nVerifying {mod} patches...");
            await RunProcess(addons, _patcher, $"verify {args}", _patcherTimeout, true);
            StepLogger.LogSurround($"Verified {mod} patches");

            StepLogger.LogSurround($"\nRemoving signatures of patched {mod} PBOs...");
            var removed = InvalidateSignatures(addons, before) + RemoveUpstreamSignaturesOfPatchedPbos(addons, state);
            StepLogger.LogSurround($"Removed {removed} signatures");
        }
    }

    internal List<string> PatchedMods()
    {
        return ModFolders(PatchesPath).Union(ModFolders(StatePath), StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal void MoveUngroupedStateToDependencies()
    {
        var files = Directory.GetFiles(StatePath).Where(x => x.EndsWith(".state.json") || x.EndsWith(".pristine")).ToList();
        if (files.Count == 0)
        {
            return;
        }

        var target = Path.Join(StatePath, DependenciesMod);
        Directory.CreateDirectory(target);
        foreach (var file in files)
        {
            File.Move(file, Path.Join(target, Path.GetFileName(file)));
        }

        StepLogger.Log($"Moved {files.Count} patcher state files into {target}");
    }

    internal static Dictionary<string, (long Length, DateTime WriteTime)> SnapshotPbos(string addons)
    {
        return Directory.GetFiles(addons, "*.pbo").ToDictionary(x => x, x => (new FileInfo(x).Length, File.GetLastWriteTimeUtc(x)));
    }

    internal int InvalidateSignatures(string addons, Dictionary<string, (long Length, DateTime WriteTime)> before)
    {
        var removed = 0;
        foreach (var (pbo, now) in SnapshotPbos(addons))
        {
            if (before.TryGetValue(pbo, out var was) && was == now)
            {
                continue;
            }

            foreach (var bisign in Directory.GetFiles(addons, $"{Path.GetFileName(pbo)}.*.bisign"))
            {
                File.Delete(bisign);
                StepLogger.Log($"Removed {Path.GetFileName(bisign)}");
                removed++;
            }
        }

        return removed;
    }

    internal int RemoveUpstreamSignaturesOfPatchedPbos(string addons, string state)
    {
        var removed = 0;
        foreach (var stateFile in Directory.GetFiles(state, "*.state.json"))
        {
            var pbo = Path.GetFileName(stateFile)[..^".state.json".Length];
            foreach (var bisign in Directory.GetFiles(addons, $"{pbo}.*.bisign")
                                            .Where(x => !Path.GetFileName(x).StartsWith($"{pbo}.uksf_dependencies_", StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(bisign);
                StepLogger.Log($"Removed upstream signature {Path.GetFileName(bisign)}");
                removed++;
            }
        }

        return removed;
    }

    private static IEnumerable<string> ModFolders(string path)
    {
        return Directory.Exists(path) ? Directory.GetDirectories(path, "@*").Select(Path.GetFileName) : [];
    }

    private static int Files(string path, string pattern)
    {
        return Directory.Exists(path) ? Directory.GetFiles(path, pattern).Length : 0;
    }
}
