namespace UKSF.Api.ArmaServer.Models;

public enum GameDataExportStatus
{
    Pending,
    Running,
    Success,
    PartialSuccess,
    FailedTimeout,
    FailedNoOutput,
    FailedTruncated,
    FailedLaunch
}
