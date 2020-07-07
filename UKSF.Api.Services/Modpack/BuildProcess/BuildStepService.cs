using System;
using System.Collections.Generic;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Modpack.BuildProcess.Steps;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildStepService : IBuildStepService {
        private readonly Dictionary<string, Type> buildStepDictionary = new Dictionary<string, Type> { { BuildStep0Prep.NAME, typeof(BuildStep0Prep) }, { BuildStep1dSource.NAME, typeof(BuildStep1dSource) } };

        public IBuildStep ResolveBuildStep(string buildStepName) {
            if (!buildStepDictionary.ContainsKey(buildStepName)) {
                throw new NullReferenceException($"Build step '{buildStepName}' does not exist in build step dictionary");
            }

            Type type = buildStepDictionary[buildStepName];
            IBuildStep step = Activator.CreateInstance(type) as IBuildStep;
            return step;
        }

        public List<ModpackBuildStep> GetStepsForRc() => new List<ModpackBuildStep> { new ModpackBuildStep(0, BuildStep0Prep.NAME), new ModpackBuildStep(1, BuildStep1dSource.NAME) };

        public List<ModpackBuildStep> GetStepsForRelease() => new List<ModpackBuildStep> { new ModpackBuildStep(0, BuildStep0Prep.NAME) };

        public List<ModpackBuildStep> GetStepsForBuild() => new List<ModpackBuildStep> { new ModpackBuildStep(0, BuildStep0Prep.NAME), new ModpackBuildStep(1, BuildStep1dSource.NAME) };
    }
}
