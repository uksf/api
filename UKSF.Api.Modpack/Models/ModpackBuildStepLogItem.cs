using System.Collections.Generic;

namespace UKSF.Api.Modpack.Models
{
    public class ModpackBuildStepLogItem
    {
        public string Colour;
        public string Text;
    }

    public class ModpackBuildStepLogItemUpdate
    {
        public bool Inline;
        public List<ModpackBuildStepLogItem> Logs;
    }
}
