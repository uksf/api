using System;
using System.Collections.Generic;

namespace UKSF.Api.Teamspeak.Models {
    public class TeamspeakServerSnapshot {
        public DateTime timestamp;
        public HashSet<TeamspeakClient> users;
    }
}
