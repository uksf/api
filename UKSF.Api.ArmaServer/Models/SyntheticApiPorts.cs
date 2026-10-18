namespace UKSF.Api.ArmaServer.Models;

public static class SyntheticApiPorts
{
    public const int GameDataExport = 3303;
    public const int DevRun = 3305;
    public const int DevRunMaxHeadlessClients = 3;

    private static readonly HashSet<int> Reserved = [GameDataExport, DevRun, .. Enumerable.Range(DevRun + 1, DevRunMaxHeadlessClients)];

    public static bool IsSynthetic(int apiPort) => apiPort > 0 && Reserved.Contains(apiPort);
}
