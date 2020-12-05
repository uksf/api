using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UKSF.Api.Teamspeak.Models {
    public class TeamspeakServerGroupUpdate {
        public List<double> ServerGroups { get; set; } = new();
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public Task DelayedProcessTask { get; set; }
    }
}
