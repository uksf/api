using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UKSF.Api.Teamspeak.Models {
    public class TeamspeakServerGroupUpdate {
        public List<double> ServerGroups = new();
        public CancellationTokenSource CancellationTokenSource;
        public Task DelayedProcessTask;
    }
}
