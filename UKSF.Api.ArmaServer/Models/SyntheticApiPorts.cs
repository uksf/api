namespace UKSF.Api.ArmaServer.Models;

public static class SyntheticApiPorts
{
    public const int ConfigExport = 3303;
    public const int DevRun = 3305;

    private static readonly HashSet<int> Reserved = [ConfigExport, DevRun];

    public static bool IsSynthetic(int apiPort) => apiPort > 0 && Reserved.Contains(apiPort);
}
