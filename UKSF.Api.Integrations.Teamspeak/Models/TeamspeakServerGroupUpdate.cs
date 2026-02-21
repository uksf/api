namespace UKSF.Api.Integrations.Teamspeak.Models;

public class TeamspeakServerGroupUpdate
{
    public readonly object Lock = new();
    public CancellationTokenSource CancellationTokenSource { get; set; }
    public Task DelayedProcessTask { get; set; }
    public List<int> ServerGroups { get; set; } = new();
}
