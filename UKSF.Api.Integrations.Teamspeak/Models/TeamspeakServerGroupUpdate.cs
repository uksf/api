using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UKSF.Api.Teamspeak.Models {
    public class TeamspeakServerGroupUpdate {
        public readonly List<double> serverGroups = new List<double>();
        public CancellationTokenSource cancellationTokenSource;
        public Task delayedProcessTask;
    }
}
