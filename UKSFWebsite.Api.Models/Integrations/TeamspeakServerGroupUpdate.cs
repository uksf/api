using System.Collections.Generic;
using System.Threading;

namespace UKSFWebsite.Api.Models.Integrations {
    public class TeamspeakServerGroupUpdate {
        public readonly List<double> serverGroups = new List<double>();
        public CancellationTokenSource cancellationTokenSource;
    }
}
