using System.Collections.Generic;

namespace UKSF.Api.Shared.Models {
    public class TeamspeakMessageEventModel {
        public TeamspeakMessageEventModel(IEnumerable<double> clientDbIds, string message) {
            ClientDbIds = clientDbIds;
            Message = message;
        }

        public IEnumerable<double> ClientDbIds { get; }
        public string Message { get; }
    }
}
