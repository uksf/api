using System;
using System.Collections.Generic;
using UKSF.Api.Models.Integrations.Github;

namespace UKSF.Api.Models.Modpack {
    public class ModpackBuild {
        public DateTime timestamp = DateTime.Now;
        public int buildNumber;
        public GithubPushEvent pushEvent;
        public bool isReleaseCandidate;
        public bool isNewVersion;
        public List<ModpackBuildStep> steps = new List<ModpackBuildStep>();
    }
}
