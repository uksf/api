using System.Collections.Generic;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IBuildStepService {
        List<ModpackBuildStep> GetStepsForNewVersion();
        List<ModpackBuildStep> GetStepsForRc();
        List<ModpackBuildStep> GetStepsForRelease();
        List<ModpackBuildStep> GetStepsForBuild();
        IBuildStep ResolveBuildStep(string buildStepName);
    }
}
