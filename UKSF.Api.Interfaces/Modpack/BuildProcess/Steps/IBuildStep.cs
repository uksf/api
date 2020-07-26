using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess.Steps {
    public interface IBuildStep {
        void Init(ModpackBuild modpackBuild, ModpackBuildStep modpackBuildStep, Func<Task> updateCallback, Action logEvent, CancellationTokenSource cancellationTokenSource);
        Task Start();
        bool CheckGuards();
        Task Setup();
        Task Process();
        Task Teardown();
        Task Succeed();
        Task Fail(Exception exception);
        Task Cancel();
        void Warning(string message);
        Task Skip();
    }
}
