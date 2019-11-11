using System;
using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Integrations {
    public class TeamspeakServerSnapshot {
        public DateTime timestamp;
        public HashSet<TeamspeakClientSnapshot> users;
    }
}
