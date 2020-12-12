using System.Collections.Generic;

namespace UKSF.Api.Shared.Models {
    public class TeamspeakMessageEventData {
        public TeamspeakMessageEventData(IEnumerable<double> clientDbIds, string message) {
            ClientDbIds = clientDbIds;
            Message = message;
        }

        public IEnumerable<double> ClientDbIds;
        public string Message;
    }
}
