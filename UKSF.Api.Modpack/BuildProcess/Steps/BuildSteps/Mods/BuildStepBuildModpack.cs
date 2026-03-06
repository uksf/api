using System.Text.RegularExpressions;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildModpack : ModBuildStep
{
    public const string Name = "Build UKSF";
    private const string ModName = "modpack";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for UKSF");

        var rootPath = Path.Join(GetBuildSourcesPath(), ModName);
        var extensionPath = Path.Join(rootPath, "extension");
        var hemttReleasePath = Path.Join(rootPath, ".hemttout", "release");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf");

        var configuration = GetEnvironmentVariable<string>("configuration");
        if (string.IsNullOrEmpty(configuration))
        {
            throw new Exception("Configuration not set for build");
        }

        StepLogger.Log($"\nConfiguration set to '{configuration}'");

        StepLogger.LogSurround("\nSetting extension version...");
        var cargoTomlPath = Path.Join(extensionPath, "Cargo.toml");
        var cargoToml = await File.ReadAllTextAsync(cargoTomlPath);
        cargoToml = Regex.Replace(cargoToml, @"^version\s*=\s*""[^""]*""", $"version = \"{Build.Version}\"", RegexOptions.Multiline);
        await File.WriteAllTextAsync(cargoTomlPath, cargoToml);
        StepLogger.Log($"Set extension version to '{Build.Version}'");
        StepLogger.LogSurround("Set extension version");

        StepLogger.LogSurround("\nBuilding Rust extension...");
        await RunProcess(
            extensionPath,
            "cmd.exe",
            "/c \"cargo build --release\"",
            (int)TimeSpan.FromMinutes(5).TotalMilliseconds,
            true,
            redirectStderrToOutput: true
        );
        StepLogger.LogSurround("Rust extension built");

        StepLogger.LogSurround("\nCopying extension DLL...");
        var builtDll = Path.Join(extensionPath, "target", "release", "uksf_x64.dll");
        var targetDll = Path.Join(rootPath, "uksf_x64.dll");
        File.Copy(builtDll, targetDll, true);
        StepLogger.Log($"Copied {builtDll} to {targetDll}");
        StepLogger.LogSurround("Copied extension DLL");

        StepLogger.LogSurround("\nSetting configuration...");
        var configurationFile = Path.Join(rootPath, "addons", "main", "script_configuration.hpp");
        await File.WriteAllTextAsync(configurationFile, $"#define CONFIGURATION '{configuration}'\n");
        StepLogger.Log($"Set configuration to '{configuration}'");
        StepLogger.LogSurround("Set configuration");

        StepLogger.LogSurround("\nRunning hemtt release...");
        await RunProcess(rootPath, "cmd.exe", HemttCommand("release --no-archive"), (int)TimeSpan.FromMinutes(5).TotalMilliseconds, true);
        StepLogger.LogSurround("Hemtt release complete");

        StepLogger.LogSurround("\nMoving UKSF release to build...");
        await CopyDirectory(hemttReleasePath, buildPath);
        StepLogger.LogSurround("Moved UKSF release to build");

        if (Build.Environment == GameEnvironment.Rc)
        {
            StepLogger.LogSurround("\nMoving RC optional...");
            await MoveRcOptional(buildPath);
            StepLogger.LogSurround("Moved RC optionals");
        }

        var optionalsPath = Path.Join(buildPath, "optionals");
        if (Directory.Exists(optionalsPath))
        {
            StepLogger.LogSurround("\nRemoving optionals folder...");
            Directory.Delete(optionalsPath, true);
            StepLogger.LogSurround("Removed optionals folder");
        }
    }

    private Task MoveRcOptional(string buildPath)
    {
        DirectoryInfo addons = new(Path.Join(buildPath, "addons"));
        DirectoryInfo optional = new(Path.Join(buildPath, "optionals", "@uksf_rc", "addons"));

        var files = GetDirectoryContents(optional);
        return CopyFiles(optional, addons, files);
    }
}
