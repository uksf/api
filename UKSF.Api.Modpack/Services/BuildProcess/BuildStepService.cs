using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.BuildProcess.Steps;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.Common;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps;

namespace UKSF.Api.Modpack.Services.BuildProcess {
    public interface IBuildStepService {
        void RegisterBuildSteps();
        List<ModpackBuildStep> GetSteps(GameEnvironment environment);
        ModpackBuildStep GetRestoreStepForRelease();
        IBuildStep ResolveBuildStep(string buildStepName);
    }

    public class BuildStepService : IBuildStepService {
        private Dictionary<string, Type> _buildStepDictionary = new Dictionary<string, Type>();

        public void RegisterBuildSteps() {
            _buildStepDictionary = AppDomain.CurrentDomain.GetAssemblies()
                                            .SelectMany(x => x.GetTypes(), (_, type) => new { type })
                                            .Select(x => new { x.type, attributes = x.type.GetCustomAttributes(typeof(BuildStepAttribute), true) })
                                            .Where(x => x.attributes.Length > 0)
                                            .Select(x => new { Key = x.attributes.Cast<BuildStepAttribute>().First().Name, Value = x.type })
                                            .ToDictionary(x => x.Key, x => x.Value);
        }

        public List<ModpackBuildStep> GetSteps(GameEnvironment environment) {
            List<ModpackBuildStep> steps = environment switch {
                GameEnvironment.RELEASE => GetStepsForRelease(),
                GameEnvironment.RC      => GetStepsForRc(),
                GameEnvironment.DEV     => GetStepsForBuild(),
                _                       => throw new ArgumentException("Invalid build environment")
            };
            ResolveIndices(steps);
            return steps;
        }

        public ModpackBuildStep GetRestoreStepForRelease() => new ModpackBuildStep(BuildStepRestore.NAME);

        public IBuildStep ResolveBuildStep(string buildStepName) {
            if (!_buildStepDictionary.ContainsKey(buildStepName)) {
                throw new NullReferenceException($"Build step '{buildStepName}' does not exist in build step dictionary");
            }

            Type type = _buildStepDictionary[buildStepName];
            IBuildStep step = Activator.CreateInstance(type) as IBuildStep;
            return step;
        }

        private static List<ModpackBuildStep> GetStepsForBuild() =>
            new List<ModpackBuildStep> {
                new ModpackBuildStep(BuildStepPrep.NAME),
                new ModpackBuildStep(BuildStepClean.NAME),
                new ModpackBuildStep(BuildStepSources.NAME),
                new ModpackBuildStep(BuildStepBuildAce.NAME),
                new ModpackBuildStep(BuildStepBuildAcre.NAME),
                new ModpackBuildStep(BuildStepBuildF35.NAME),
                new ModpackBuildStep(BuildStepBuildModpack.NAME),
                new ModpackBuildStep(BuildStepIntercept.NAME),
                new ModpackBuildStep(BuildStepExtensions.NAME),
                new ModpackBuildStep(BuildStepSignDependencies.NAME),
                new ModpackBuildStep(BuildStepDeploy.NAME),
                new ModpackBuildStep(BuildStepKeys.NAME),
                new ModpackBuildStep(BuildStepCbaSettings.NAME),
                new ModpackBuildStep(BuildStepBuildRepo.NAME)
            };

        private static List<ModpackBuildStep> GetStepsForRc() =>
            new List<ModpackBuildStep> {
                new ModpackBuildStep(BuildStepPrep.NAME),
                new ModpackBuildStep(BuildStepClean.NAME),
                new ModpackBuildStep(BuildStepSources.NAME),
                new ModpackBuildStep(BuildStepBuildAce.NAME),
                new ModpackBuildStep(BuildStepBuildAcre.NAME),
                new ModpackBuildStep(BuildStepBuildF35.NAME),
                new ModpackBuildStep(BuildStepBuildModpack.NAME),
                new ModpackBuildStep(BuildStepIntercept.NAME),
                new ModpackBuildStep(BuildStepExtensions.NAME),
                new ModpackBuildStep(BuildStepSignDependencies.NAME),
                new ModpackBuildStep(BuildStepDeploy.NAME),
                new ModpackBuildStep(BuildStepKeys.NAME),
                new ModpackBuildStep(BuildStepCbaSettings.NAME),
                new ModpackBuildStep(BuildStepBuildRepo.NAME),
                new ModpackBuildStep(BuildStepNotify.NAME)
            };

        private static List<ModpackBuildStep> GetStepsForRelease() =>
            new List<ModpackBuildStep> {
                new ModpackBuildStep(BuildStepClean.NAME),
                new ModpackBuildStep(BuildStepBackup.NAME),
                new ModpackBuildStep(BuildStepDeploy.NAME),
                new ModpackBuildStep(BuildStepReleaseKeys.NAME),
                new ModpackBuildStep(BuildStepCbaSettings.NAME),
                new ModpackBuildStep(BuildStepBuildRepo.NAME),
                new ModpackBuildStep(BuildStepPublish.NAME),
                new ModpackBuildStep(BuildStepNotify.NAME),
                new ModpackBuildStep(BuildStepMerge.NAME)
            };

        private static void ResolveIndices(IReadOnlyList<ModpackBuildStep> steps) {
            for (int i = 0; i < steps.Count; i++) {
                steps[i].Index = i;
            }
        }
    }
}
