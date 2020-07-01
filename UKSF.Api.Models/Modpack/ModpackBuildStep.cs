using System;
using System.Collections.Generic;

namespace UKSF.Api.Models.Modpack {
    public class ModpackBuildStep {
        public int index;
        public DateTime startTime = DateTime.Parse("20000101");
        public DateTime endTime = DateTime.Parse("20000101");
        public string name;
        public bool running;
        public bool success;
        public bool fail;
        public List<string> logs = new List<string>();
    }
}
