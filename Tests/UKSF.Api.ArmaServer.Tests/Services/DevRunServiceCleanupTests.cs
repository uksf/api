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
    public void Trigger_cleans_up_previous_runs_before_launching_and_logs_each_failed_deletion()
    {
        var calls = new List<string>();
        var launcher = new Mock<IDevRunLauncher>();
        launcher.Setup(x => x.CleanUpPreviousRuns()).Callback(() => calls.Add("cleanup")).Returns(["C:/configs/DevRun_old00001.cfg: locked"]);
        launcher.Setup(x => x.Launch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), null, 0))
                .Callback(() => calls.Add("launch"))
                .Returns(new SyntheticLaunchResult(4321, "profile", "mission"));
        var gate = new Mock<IArmaSyntheticLaunchGate>();
        gate.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(true);
        var context = new Mock<IDevRunsContext>();
        context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, object>>>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        var processUtilities = new Mock<IProcessUtilities>();
        processUtilities.Setup(x => x.IsProcessAlive(4321)).Returns(true);
        var logger = new Mock<IUksfLogger>();

        var sut = new DevRunService(
            launcher.Object,
            context.Object,
            processUtilities.Object,
            gate.Object,
            new Mock<IVariablesService>().Object,
            logger.Object,
            10,
            5
        );
        sut.Trigger("diag_log 1;", ["@mod"], null);

        calls.Should().Equal("cleanup", "launch");
        logger.Verify(x => x.LogWarning("Dev run cleanup could not delete C:/configs/DevRun_old00001.cfg: locked"), Times.Once);
    }
}
