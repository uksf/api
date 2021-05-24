using System.Collections.Generic;

namespace UKSF.Api.Shared.Models
{
    public class TeamspeakMessageEventData
    {
        public IEnumerable<int> ClientDbIds;
        public string Message;

        public TeamspeakMessageEventData(IEnumerable<int> clientDbIds, string message)
        {
            ClientDbIds = clientDbIds;
            Message = message;
        }
    }
}
