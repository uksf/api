using System.Collections.Generic;

namespace UKSF.Api.Shared.Models;

public class TeamspeakMessageEventData
{
    public IEnumerable<int> ClientDbIds { get; set; }
    public string Message { get; set; }

    public TeamspeakMessageEventData(IEnumerable<int> clientDbIds, string message)
    {
        ClientDbIds = clientDbIds;
        Message = message;
    }
}
