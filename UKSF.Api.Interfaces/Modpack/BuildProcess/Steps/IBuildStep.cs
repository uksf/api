using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess.Steps {
    public interface IBuildStep {
        void Init(ModpackBuild modpackBuild, ModpackBuildStep modpackBuildStep, Func<Task> updateCallback, CancellationTokenSource cancellationTokenSource);
        Task Start();
        Task<bool> CheckGuards();
        Task Setup();
        Task Process();
        Task Teardown();
        Task Succeed();
        Task Fail(Exception exception);
        Task Cancel();
        Task Warning(string message);
        Task Skip();
    }
}
