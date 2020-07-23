using System;
using System.Collections.Generic;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Modpack.BuildProcess.Steps;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildStepService : IBuildStepService {
        private readonly Dictionary<string, Type> buildStepDictionary = new Dictionary<string, Type> {
            { BuildStep0Prep.NAME, typeof(BuildStep0Prep) },
            { BuildStep1Source.NAME, typeof(BuildStep1Source) },
            { BuildStep2Build.NAME, typeof(BuildStep2Build) },
            { BuildStep90PublishRelease.NAME, typeof(BuildStep90PublishRelease) },
            { BuildStep95Notify.NAME, typeof(BuildStep95Notify) },
            { BuildStep99MergeRelease.NAME, typeof(BuildStep99MergeRelease) }
        };

        public IBuildStep ResolveBuildStep(string buildStepName) {
            if (!buildStepDictionary.ContainsKey(buildStepName)) {
                throw new NullReferenceException($"Build step '{buildStepName}' does not exist in build step dictionary");
            }

            Type type = buildStepDictionary[buildStepName];
            IBuildStep step = Activator.CreateInstance(type) as IBuildStep;
            return step;
        }

        public List<ModpackBuildStep> GetStepsForBuild() {
            List<ModpackBuildStep> steps = new List<ModpackBuildStep> {
                new ModpackBuildStep(BuildStep0Prep.NAME),
                new ModpackBuildStep(BuildStep1Source.NAME),
                new ModpackBuildStep(BuildStep2Build.NAME),
                new ModpackBuildStep(BuildStep95Notify.NAME)
            };
            ResolveIndices(steps);
            return steps;
        }

        public List<ModpackBuildStep> GetStepsForRc() {
            List<ModpackBuildStep> steps = new List<ModpackBuildStep> {
                new ModpackBuildStep(BuildStep0Prep.NAME), new ModpackBuildStep(BuildStep1Source.NAME), new ModpackBuildStep(BuildStep2Build.NAME), new ModpackBuildStep(BuildStep95Notify.NAME)
            };
            ResolveIndices(steps);
            return steps;
        }

        public List<ModpackBuildStep> GetStepsForRelease() {
            List<ModpackBuildStep> steps = new List<ModpackBuildStep> {
                new ModpackBuildStep(BuildStep0Prep.NAME),
                new ModpackBuildStep(BuildStep90PublishRelease.NAME),
                new ModpackBuildStep(BuildStep95Notify.NAME),
                new ModpackBuildStep(BuildStep99MergeRelease.NAME)
            };
            ResolveIndices(steps);
            return steps;
        }

        private static void ResolveIndices(IReadOnlyList<ModpackBuildStep> steps) {
            for (int i = 0; i < steps.Count; i++) {
                steps[i].index = i;
            }
        }
    }
}
