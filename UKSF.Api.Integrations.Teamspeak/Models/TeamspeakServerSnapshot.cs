using System;
using System.Collections.Generic;

namespace UKSF.Api.Teamspeak.Models {
    public class TeamspeakServerSnapshot {
        public DateTime Timestamp;
        public HashSet<TeamspeakClient> Users;
    }
}
