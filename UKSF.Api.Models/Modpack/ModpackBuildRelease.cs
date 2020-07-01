using System.Collections.Generic;

namespace UKSF.Api.Models.Modpack {
    public class ModpackBuildRelease : DatabaseObject {
        public string version;
        public List<ModpackBuild> builds = new List<ModpackBuild>();
    }
}
