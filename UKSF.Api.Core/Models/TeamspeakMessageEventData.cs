namespace UKSF.Api.Core.Models;

public class TeamspeakMessageEventData(IEnumerable<int> clientDbIds, string message) : EventData
{
    public IEnumerable<int> ClientDbIds { get; } = clientDbIds;
    public string Message { get; } = message;
}
