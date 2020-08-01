using System.Collections.Generic;

namespace UKSF.Api.Models.Modpack {
    public class ModpackBuildStepLogItem {
        public string text;
        public string colour;
    }

    public class ModpackBuildStepLogItemUpdate {
        public bool inline;
        public List<ModpackBuildStepLogItem> logs;
    }
}
