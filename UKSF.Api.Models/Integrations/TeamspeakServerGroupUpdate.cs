using System.Collections.Generic;
using System.Threading;

namespace UKSF.Api.Models.Integrations {
    public class TeamspeakServerGroupUpdate {
        public readonly List<double> serverGroups = new List<double>();
        public CancellationTokenSource cancellationTokenSource;
    }
}
