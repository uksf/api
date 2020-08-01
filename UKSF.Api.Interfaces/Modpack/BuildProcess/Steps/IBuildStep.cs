using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess.Steps {
    public interface IBuildStep {
        void Init(ModpackBuild modpackBuild, ModpackBuildStep modpackBuildStep, Func<UpdateDefinition<ModpackBuild>, Task> buildUpdateCallback, Func<Task> stepUpdateCallback, CancellationTokenSource cancellationTokenSource);
        Task Start();
        bool CheckGuards();
        Task Setup();
        Task Process();
        Task Succeed();
        Task Fail(Exception exception);
        Task Cancel();
        void Warning(string message);
        Task Skip();
    }
}
