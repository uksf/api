using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerOfflineLogTests : GameServerProcessManagerTestBase
{
    private const string OfflineMessage = "Process monitor detected server 'Test' is offline";

    private DomainGameServer GivenServerWithOtherArmaProcessRunning(int? processId)
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = processId,
            HeadlessClients = [],
            Status = new GameServerStatus { Running = processId is not null }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Other.cfg -port=2310 ")]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        return server;
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenServerWasNotRunning_DoesNotLogOffline()
    {
        GivenServerWithOtherArmaProcessRunning(processId: null);

        await _sut.GetAllServerStatusesAsync();

        _mockLogger.Verify(x => x.LogInfo(OfflineMessage), Times.Never);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenTrackedServerProcessIsGone_LogsOffline()
    {
        GivenServerWithOtherArmaProcessRunning(processId: 1234);

        await _sut.GetAllServerStatusesAsync();

        _mockLogger.Verify(x => x.LogInfo(OfflineMessage), Times.Once);
    }
}
