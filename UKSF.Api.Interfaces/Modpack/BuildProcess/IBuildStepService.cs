using System.Collections.Generic;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IBuildStepService {
        void RegisterBuildSteps();
        List<ModpackBuildStep> GetSteps(GameEnvironment environment);
        ModpackBuildStep GetRestoreStepForRelease();
        IBuildStep ResolveBuildStep(string buildStepName);
    }
}
