namespace UKSF.Api.ArmaServer.Services;

public record GameDataExportLaunchResult(
    int ProcessId,
    string ExpectedOutputDirectory,
    string ConfigGlob,
    string CbaSettingsGlob,
    string CbaSettingsReferenceGlob
);

public interface IGameDataExportProcessLauncher
{
    GameDataExportLaunchResult Launch(string modpackVersion);
}
