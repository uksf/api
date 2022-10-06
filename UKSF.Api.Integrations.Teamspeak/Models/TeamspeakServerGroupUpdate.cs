namespace UKSF.Api.Teamspeak.Models;

public class TeamspeakServerGroupUpdate
{
    public CancellationTokenSource CancellationTokenSource { get; set; }
    public Task DelayedProcessTask { get; set; }
    public List<int> ServerGroups { get; set; } = new();
}
