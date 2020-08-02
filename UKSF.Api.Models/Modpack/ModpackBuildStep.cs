using System;
using System.Collections.Generic;
using System.Globalization;

namespace UKSF.Api.Models.Modpack {
    public class ModpackBuildStep {
        public ModpackBuildResult buildResult = ModpackBuildResult.NONE;
        public DateTime endTime = DateTime.ParseExact("20000101", "yyyyMMdd", CultureInfo.InvariantCulture);
        public bool finished;
        public int index;
        public List<ModpackBuildStepLogItem> logs = new List<ModpackBuildStepLogItem>();
        public string name;
        public bool running;
        public DateTime startTime = DateTime.ParseExact("20000101", "yyyyMMdd", CultureInfo.InvariantCulture);

        public ModpackBuildStep(string name) => this.name = name;
    }
}
