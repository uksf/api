using System;

namespace UKSF.Api.Models.Modpack {
    public class ModpackRelease : DatabaseObject {
        public DateTime timestamp;
        public string version;
        public string description;
        public string changelog;
    }
}
