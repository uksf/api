using System.Collections.Generic;

namespace UKSF.Api.Shared.Models {
    public class TeamspeakMessageEventModel {
        public IEnumerable<double> ClientDbIds { get; }
        public string Message { get; }

        public TeamspeakMessageEventModel(IEnumerable<double> clientDbIds, string message) {
            ClientDbIds = clientDbIds;
            Message = message;
        }
    }
}
