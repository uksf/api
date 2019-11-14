using System.Collections.Generic;
using System.Threading;

namespace UKSFWebsite.Api.Models.Integrations {
    public class TeamspeakServerGroupUpdate {
        public readonly List<string> serverGroups = new List<string>();
        public CancellationTokenSource cancellationTokenSource;
    }
}
