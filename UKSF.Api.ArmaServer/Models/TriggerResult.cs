namespace UKSF.Api.ArmaServer.Models;

public enum TriggerOutcome
{
    Started,
    AlreadyRunning
}

public record TriggerResult(TriggerOutcome Outcome, string RunId);
