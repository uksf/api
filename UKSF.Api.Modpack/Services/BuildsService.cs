using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.BuildProcess;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.Services
{
    public interface IBuildsService
    {
        IEnumerable<ModpackBuild> GetDevBuilds();
        IEnumerable<ModpackBuild> GetRcBuilds();
        ModpackBuild GetLatestDevBuild();
        ModpackBuild GetLatestRcBuild(string version);
        Task UpdateBuild(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition);
        Task UpdateBuildStep(ModpackBuild build, ModpackBuildStep buildStep);
        Task<ModpackBuild> CreateDevBuild(string version, GithubCommit commit, NewBuild newBuild = null);
        Task<ModpackBuild> CreateRcBuild(string version, GithubCommit commit);
        Task<ModpackBuild> CreateReleaseBuild(string version);
        Task SetBuildRunning(ModpackBuild build);
        Task SucceedBuild(ModpackBuild build);
        Task FailBuild(ModpackBuild build);
        Task CancelBuild(ModpackBuild build);
        Task<ModpackBuild> CreateRebuild(ModpackBuild build, string newSha = "");
        void CancelInterruptedBuilds();
    }

    public class BuildsService : IBuildsService
    {
        private readonly IAccountContext _accountContext;
        private readonly IBuildsContext _buildsContext;
        private readonly IBuildStepService _buildStepService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public BuildsService(IBuildsContext buildsContext, IBuildStepService buildStepService, IAccountContext accountContext, IHttpContextService httpContextService, ILogger logger)
        {
            _buildsContext = buildsContext;
            _buildStepService = buildStepService;
            _accountContext = accountContext;
            _httpContextService = httpContextService;
            _logger = logger;
        }

        public async Task UpdateBuild(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition)
        {
            await _buildsContext.Update(build, updateDefinition);
        }

        public async Task UpdateBuildStep(ModpackBuild build, ModpackBuildStep buildStep)
        {
            await _buildsContext.Update(build, buildStep);
        }

        public IEnumerable<ModpackBuild> GetDevBuilds()
        {
            return _buildsContext.Get(x => x.Environment == GameEnvironment.DEV);
        }

        public IEnumerable<ModpackBuild> GetRcBuilds()
        {
            return _buildsContext.Get(x => x.Environment != GameEnvironment.DEV);
        }

        public ModpackBuild GetLatestDevBuild()
        {
            return GetDevBuilds().FirstOrDefault();
        }

        public ModpackBuild GetLatestRcBuild(string version)
        {
            return GetRcBuilds().FirstOrDefault(x => x.Version == version);
        }

        public async Task<ModpackBuild> CreateDevBuild(string version, GithubCommit commit, NewBuild newBuild = null)
        {
            var previousBuild = GetLatestDevBuild();
            var builderId = _accountContext.GetSingle(x => x.Email == commit.Author)?.Id;
            ModpackBuild build = new()
            {
                Version = version,
                BuildNumber = previousBuild?.BuildNumber + 1 ?? 1,
                Environment = GameEnvironment.DEV,
                Commit = commit,
                BuilderId = builderId,
                Steps = _buildStepService.GetSteps(GameEnvironment.DEV)
            };

            if (previousBuild != null)
            {
                SetEnvironmentVariables(build, previousBuild, newBuild);
            }

            await _buildsContext.Add(build);
            return build;
        }

        public async Task<ModpackBuild> CreateRcBuild(string version, GithubCommit commit)
        {
            var previousBuild = GetLatestRcBuild(version);
            var builderId = _accountContext.GetSingle(x => x.Email == commit.Author)?.Id;
            ModpackBuild build = new()
            {
                Version = version,
                BuildNumber = previousBuild?.BuildNumber + 1 ?? 1,
                Environment = GameEnvironment.RC,
                Commit = commit,
                BuilderId = builderId,
                Steps = _buildStepService.GetSteps(GameEnvironment.RC)
            };

            if (previousBuild != null)
            {
                SetEnvironmentVariables(build, previousBuild);
            }

            await _buildsContext.Add(build);
            return build;
        }

        public async Task<ModpackBuild> CreateReleaseBuild(string version)
        {
            // There must be at least one RC build to release
            var previousBuild = GetRcBuilds().FirstOrDefault(x => x.Version == version);
            if (previousBuild == null)
            {
                throw new InvalidOperationException("Release build requires at least one RC build");
            }

            ModpackBuild build = new()
            {
                Version = version,
                BuildNumber = previousBuild.BuildNumber + 1,
                Environment = GameEnvironment.RELEASE,
                Commit = previousBuild.Commit,
                BuilderId = _httpContextService.GetUserId(),
                Steps = _buildStepService.GetSteps(GameEnvironment.RELEASE)
            };
            build.Commit.Message = "Release deployment (no content changes)";
            await _buildsContext.Add(build);
            return build;
        }

        public async Task<ModpackBuild> CreateRebuild(ModpackBuild build, string newSha = "")
        {
            var latestBuild = build.Environment == GameEnvironment.DEV ? GetLatestDevBuild() : GetLatestRcBuild(build.Version);
            ModpackBuild rebuild = new()
            {
                Version = latestBuild.Environment == GameEnvironment.DEV ? null : latestBuild.Version,
                BuildNumber = latestBuild.BuildNumber + 1,
                IsRebuild = true,
                Environment = latestBuild.Environment,
                Steps = _buildStepService.GetSteps(build.Environment),
                Commit = latestBuild.Commit,
                BuilderId = _httpContextService.GetUserId(),
                EnvironmentVariables = latestBuild.EnvironmentVariables
            };
            if (!string.IsNullOrEmpty(newSha))
            {
                rebuild.Commit.After = newSha;
            }

            rebuild.Commit.Message = latestBuild.Environment == GameEnvironment.RELEASE
                ? $"Re-deployment of release {rebuild.Version}"
                : $"Rebuild of #{build.BuildNumber}\n\n{rebuild.Commit.Message}";
            await _buildsContext.Add(rebuild);
            return rebuild;
        }

        public async Task SetBuildRunning(ModpackBuild build)
        {
            build.Running = true;
            build.StartTime = DateTime.UtcNow;
            await _buildsContext.Update(build, Builders<ModpackBuild>.Update.Set(x => x.Running, true).Set(x => x.StartTime, DateTime.UtcNow));
        }

        public async Task SucceedBuild(ModpackBuild build)
        {
            await FinishBuild(build, build.Steps.Any(x => x.BuildResult == ModpackBuildResult.WARNING) ? ModpackBuildResult.WARNING : ModpackBuildResult.SUCCESS);
        }

        public async Task FailBuild(ModpackBuild build)
        {
            await FinishBuild(build, ModpackBuildResult.FAILED);
        }

        public async Task CancelBuild(ModpackBuild build)
        {
            await FinishBuild(build, build.Steps.Any(x => x.BuildResult == ModpackBuildResult.WARNING) ? ModpackBuildResult.WARNING : ModpackBuildResult.CANCELLED);
        }

        public void CancelInterruptedBuilds()
        {
            var builds = _buildsContext.Get(x => x.Running || x.Steps.Any(y => y.Running)).ToList();
            if (!builds.Any())
            {
                return;
            }

            var tasks = builds.Select(
                async build =>
                {
                    var runningStep = build.Steps.FirstOrDefault(x => x.Running);
                    if (runningStep != null)
                    {
                        runningStep.Running = false;
                        runningStep.Finished = true;
                        runningStep.EndTime = DateTime.UtcNow;
                        runningStep.BuildResult = ModpackBuildResult.CANCELLED;
                        runningStep.Logs.Add(new() { Text = "\nBuild was interrupted", Colour = "goldenrod" });
                        await _buildsContext.Update(build, runningStep);
                    }

                    await FinishBuild(build, ModpackBuildResult.CANCELLED);
                }
            );
            _ = Task.WhenAll(tasks);
            _logger.LogAudit($"Marked {builds.Count} interrupted builds as cancelled", "SERVER");
        }

        private async Task FinishBuild(ModpackBuild build, ModpackBuildResult result)
        {
            build.Running = false;
            build.Finished = true;
            build.BuildResult = result;
            build.EndTime = DateTime.UtcNow;
            await _buildsContext.Update(build, Builders<ModpackBuild>.Update.Set(x => x.Running, false).Set(x => x.Finished, true).Set(x => x.BuildResult, result).Set(x => x.EndTime, DateTime.UtcNow));
        }

        private static void SetEnvironmentVariables(ModpackBuild build, ModpackBuild previousBuild, NewBuild newBuild = null)
        {
            SetEnvironmentVariable(build, previousBuild, "ace_updated", "Build ACE", newBuild?.Ace ?? false);
            SetEnvironmentVariable(build, previousBuild, "acre_updated", "Build ACRE", newBuild?.Acre ?? false);
            SetEnvironmentVariable(build, previousBuild, "uksf_air_updated", "Build Air", newBuild?.Air ?? false);
        }

        private static void SetEnvironmentVariable(ModpackBuild build, ModpackBuild previousBuild, string key, string stepName, bool force)
        {
            if (force)
            {
                build.EnvironmentVariables[key] = true;
                return;
            }

            if (previousBuild.EnvironmentVariables.ContainsKey(key))
            {
                var updated = (bool)previousBuild.EnvironmentVariables[key];
                if (updated)
                {
                    var step = previousBuild.Steps.FirstOrDefault(x => x.Name == stepName);
                    if (step != null && (!step.Finished || step.BuildResult is ModpackBuildResult.FAILED or ModpackBuildResult.CANCELLED))
                    {
                        build.EnvironmentVariables[key] = true;
                    }
                }
            }
        }
    }
}
