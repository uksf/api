namespace UKSF.Api.ArmaServer.Consumers;

public record ProcessMissionStatsBatch
{
    public string Mission { get; init; } = string.Empty;
    public string Map { get; init; } = string.Empty;
    public List<string> Events { get; init; } = [];
    public DateTime ReceivedAt { get; init; }
}
