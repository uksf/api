using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UKSF.Api.Models.Integrations {
    public class TeamspeakServerGroupUpdate {
        public readonly List<double> serverGroups = new List<double>();
        public CancellationTokenSource cancellationTokenSource;
        public Task delayedProcessTask;
    }
}
