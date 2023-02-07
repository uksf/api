namespace UKSF.Api.Core.Models;

public class TeamspeakMessageEventData
{
    public TeamspeakMessageEventData(IEnumerable<int> clientDbIds, string message)
    {
        ClientDbIds = clientDbIds;
        Message = message;
    }

    public IEnumerable<int> ClientDbIds { get; set; }
    public string Message { get; set; }
}
