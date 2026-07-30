using System;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class DevRunServiceFinishOrphanTests
{
    [Fact]
    public async Task FinishAsync_releases_gate_when_record_missing_and_no_pid()
    {
        var launcher = new Mock<IDevRunLauncher>();
        var context = new Mock<IDevRunsContext>();
        var processUtilities = new Mock<IProcessUtilities>();
        var gate = new Mock<IArmaSyntheticLaunchGate>();
        var variablesService = new Mock<IVariablesService>();
        var logger = new Mock<IUksfLogger>();

        context.Setup(x => x.GetSingle(It.IsAny<Func<DomainDevRun, bool>>())).Returns((DomainDevRun)null);
        gate.SetupGet(x => x.CurrentRunId).Returns("orphan-run");

        var sut = new DevRunService(launcher.Object, context.Object, processUtilities.Object, gate.Object, variablesService.Object, logger.Object, 50, 5);

        await sut.FinishAsync("orphan-run");

        gate.Verify(x => x.Release(), Times.Once);
    }
}
