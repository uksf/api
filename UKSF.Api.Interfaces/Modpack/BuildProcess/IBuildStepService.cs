using System.Collections.Generic;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IBuildStepService {
        List<ModpackBuildStep> GetStepsForBuild();
        List<ModpackBuildStep> GetStepsForRc();
        List<ModpackBuildStep> GetStepsForRelease();
        IBuildStep ResolveBuildStep(string buildStepName);
    }
}
