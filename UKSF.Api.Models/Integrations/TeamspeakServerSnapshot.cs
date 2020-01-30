using System;
using System.Collections.Generic;

namespace UKSF.Api.Models.Integrations {
    public class TeamspeakServerSnapshot {
        public DateTime timestamp;
        public HashSet<TeamspeakClient> users;
    }
}
