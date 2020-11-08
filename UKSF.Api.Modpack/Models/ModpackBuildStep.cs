using System;
using System.Collections.Generic;
using System.Globalization;

namespace UKSF.Api.Modpack.Models {
    public class ModpackBuildStep {
        public ModpackBuildResult BuildResult = ModpackBuildResult.NONE;
        public DateTime EndTime = DateTime.ParseExact("20000101", "yyyyMMdd", CultureInfo.InvariantCulture);
        public bool Finished;
        public int Index;
        public List<ModpackBuildStepLogItem> Logs = new List<ModpackBuildStepLogItem>();
        public string Name;
        public bool Running;
        public DateTime StartTime = DateTime.ParseExact("20000101", "yyyyMMdd", CultureInfo.InvariantCulture);

        public ModpackBuildStep(string name) => Name = name;
    }
}
