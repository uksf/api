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

namespace UKSF.Api.Modpack.Services.BuildProcess
{
    public interface IBuildStepService
    {
        void RegisterBuildSteps();
        List<ModpackBuildStep> GetSteps(GameEnvironment environment);
        ModpackBuildStep GetRestoreStepForRelease();
        IBuildStep ResolveBuildStep(string buildStepName);
    }

    public class BuildStepService : IBuildStepService
    {
        private Dictionary<string, Type> _buildStepDictionary = new();

        public void RegisterBuildSteps()
        {
            _buildStepDictionary = AppDomain.CurrentDomain.GetAssemblies()
                                            .SelectMany(x => x.GetTypes(), (_, type) => new { type })
                                            .Select(x => new { x.type, attributes = x.type.GetCustomAttributes(typeof(BuildStepAttribute), true) })
                                            .Where(x => x.attributes.Length > 0)
                                            .Select(x => new { Key = x.attributes.Cast<BuildStepAttribute>().First().Name, Value = x.type })
                                            .ToDictionary(x => x.Key, x => x.Value);
        }

        public List<ModpackBuildStep> GetSteps(GameEnvironment environment)
        {
            var steps = environment switch
            {
                GameEnvironment.RELEASE => GetStepsForRelease(),
                GameEnvironment.RC      => GetStepsForRc(),
                GameEnvironment.DEV     => GetStepsForBuild(),
                _                       => throw new ArgumentException("Invalid build environment")
            };
            ResolveIndices(steps);
            return steps;
        }

        public ModpackBuildStep GetRestoreStepForRelease()
        {
            return new(BuildStepRestore.NAME);
        }

        public IBuildStep ResolveBuildStep(string buildStepName)
        {
            if (!_buildStepDictionary.ContainsKey(buildStepName))
            {
                throw new NullReferenceException($"Build step '{buildStepName}' does not exist in build step dictionary");
            }

            var type = _buildStepDictionary[buildStepName];
            var step = Activator.CreateInstance(type) as IBuildStep;
            return step;
        }

        private static List<ModpackBuildStep> GetStepsForBuild()
        {
            return new()
            {
                new(BuildStepPrep.NAME),
                new(BuildStepClean.NAME),
                new(BuildStepSources.NAME),
                new(BuildStepBuildAce.NAME),
                new(BuildStepBuildAcre.NAME),
                new(BuildStepBuildAir.NAME),
                new(BuildStepBuildModpack.NAME),
                new(BuildStepIntercept.NAME),
                new(BuildStepExtensions.NAME),
                new(BuildStepSignDependencies.NAME),
                new(BuildStepDeploy.NAME),
                new(BuildStepKeys.NAME),
                new(BuildStepCbaSettings.NAME),
                new(BuildStepBuildRepo.NAME)
            };
        }

        private static List<ModpackBuildStep> GetStepsForRc()
        {
            return new()
            {
                new(BuildStepPrep.NAME),
                new(BuildStepClean.NAME),
                new(BuildStepSources.NAME),
                new(BuildStepBuildAce.NAME),
                new(BuildStepBuildAcre.NAME),
                new(BuildStepBuildAir.NAME),
                new(BuildStepBuildModpack.NAME),
                new(BuildStepIntercept.NAME),
                new(BuildStepExtensions.NAME),
                new(BuildStepSignDependencies.NAME),
                new(BuildStepDeploy.NAME),
                new(BuildStepKeys.NAME),
                new(BuildStepCbaSettings.NAME),
                new(BuildStepBuildRepo.NAME),
                // new(BuildStepNotify.NAME)
            };
        }

        private static List<ModpackBuildStep> GetStepsForRelease()
        {
            return new()
            {
                new(BuildStepClean.NAME),
                new(BuildStepBackup.NAME),
                new(BuildStepDeploy.NAME),
                new(BuildStepReleaseKeys.NAME),
                new(BuildStepCbaSettings.NAME),
                new(BuildStepBuildRepo.NAME),
                new(BuildStepPublish.NAME),
                new(BuildStepNotify.NAME),
                new(BuildStepMerge.NAME)
            };
        }

        private static void ResolveIndices(IReadOnlyList<ModpackBuildStep> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                steps[i].Index = i;
            }
        }
    }
}
