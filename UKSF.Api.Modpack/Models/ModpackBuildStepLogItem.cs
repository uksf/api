using System.Collections.Generic;

namespace UKSF.Api.Modpack.Models {
    public class ModpackBuildStepLogItem {
        public string Text;
        public string Colour;
    }

    public class ModpackBuildStepLogItemUpdate {
        public bool Inline;
        public List<ModpackBuildStepLogItem> Logs;
    }
}
