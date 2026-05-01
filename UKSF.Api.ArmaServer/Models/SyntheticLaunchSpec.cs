namespace UKSF.Api.ArmaServer.Models;

public record SyntheticLaunchSpec(
    string ProfileName,
    string ConfigFileName,
    string MissionName,
    string ServerExecutablePath,
    int GamePort,
    int ApiPort,
    IReadOnlyList<string> Mods,
    string ServerConfig,
    string MissionSqm,
    string DescriptionExt,
    IReadOnlyDictionary<string, string> FunctionFiles,
    IReadOnlyDictionary<string, string>? MissionFiles = null
);
