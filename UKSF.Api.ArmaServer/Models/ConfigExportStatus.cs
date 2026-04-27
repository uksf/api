namespace UKSF.Api.ArmaServer.Models;

public enum ConfigExportStatus
{
    Pending,
    Running,
    Success,
    FailedTimeout,
    FailedNoOutput,
    FailedTruncated,
    FailedLaunch
}
