using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UKSF.Api.Teamspeak.Models
{
    public class TeamspeakServerGroupUpdate
    {
        public CancellationTokenSource CancellationTokenSource;
        public Task DelayedProcessTask;
        public List<int> ServerGroups = new();
    }
}
