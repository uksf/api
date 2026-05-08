using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public record DevRunTriggerResult(DevRunTriggerOutcome Outcome, string RunId, IReadOnlyList<string> MissingPaths = null);

public enum DevRunTriggerOutcome
{
    Started,
    AlreadyRunning,
    BadModPaths,
    LaunchFailed
}

public record DevRunStatusResponse(string RunId, DevRunStatus Status, DateTime? StartedAt, DateTime? CompletedAt, string ResultPreview, string FailureDetail);

public interface IDevRunService
{
    DevRunTriggerResult Trigger(string sqf, IReadOnlyList<string> mods, int? timeoutSeconds, string worldName = null);
    DevRunStatusResponse GetStatus(string runId);
    Task AppendLogAsync(string runId, string line);
    Task AppendResultAsync(string runId, string payload);
    Task FinishAsync(string runId);
}
