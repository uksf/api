using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class DevRunServiceCleanupTests
{
    [Fact]
    public async Task Watcher_cleans_up_artifacts_before_releasing_gate_when_server_exits()
    {
        var calls = new List<string>();
        var released = new TaskCompletionSource();
        var launcher = new Mock<IDevRunLauncher>();
        launcher.Setup(x => x.Launch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), null, 0))
                .Returns(new SyntheticLaunchResult(4321, "profile", "mission"));
        launcher.Setup(x => x.CleanUp()).Callback(() => calls.Add("cleanup"));
        var gate = new Mock<IArmaSyntheticLaunchGate>();
        gate.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(true);
        gate.Setup(x => x.Release())
            .Callback(() =>
                {
                    calls.Add("release");
                    released.TrySetResult();
                }
            );
        var context = new Mock<IDevRunsContext>();
        context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, object>>>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        var processUtilities = new Mock<IProcessUtilities>();
        processUtilities.Setup(x => x.IsProcessAlive(4321)).Returns(false);

        var sut = new DevRunService(
            launcher.Object,
            context.Object,
            processUtilities.Object,
            gate.Object,
            new Mock<IVariablesService>().Object,
            new Mock<IUksfLogger>().Object,
            10,
            5
        );
        sut.Trigger("diag_log 1;", ["@mod"], null);

        await released.Task.WaitAsync(TimeSpan.FromSeconds(5));

        calls.Should().Equal("cleanup", "release");
    }
}
