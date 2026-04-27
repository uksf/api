namespace UKSF.Api.ArmaServer.Services;

// Game version isn't known at trigger time; the extension names the output
// "config_<gameVer>_uksf-<modpackVer>.cpp", so we match by glob.
public record ConfigExportLaunchResult(int ProcessId, string ExpectedOutputDirectory, string ExpectedFilenameGlob);

public interface IConfigExportProcessLauncher
{
    ConfigExportLaunchResult Launch(string modpackVersion);
}
