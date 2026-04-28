namespace UKSF.Api.ArmaServer.Models;

public enum DevRunStatus
{
    Pending,
    Running,
    Success,
    FailedNoOutput,
    FailedTimeout,
    FailedLaunch,
    FailedTooLarge
}
