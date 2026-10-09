using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerOwnershipTests : GameServerProcessManagerTestBase
{
    [Fact]
    public async Task KillServerAsync_DoesNotKillAStoredHeadlessClientPidThatAnotherApiOwns()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            HeadlessClientProcessIds = [5001, 5002],
            Status = new GameServerStatus { Running = true }
        };
        _mockHelpers.Setup(x => x.IsOwnedProcessId(5002)).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);

        await _sut.KillServerAsync(server);

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(5002), Times.Never);
    }

    [Fact]
    public async Task StopServerAsync_DoesNotSendShutdownToAnApiPortNoOwnedProcessListensOn()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ApiPort = 2403,
            Status = new GameServerStatus { Running = true }
        };
        _mockHelpers.Setup(x => x.IsOwnedApiPort(2403)).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK)));

        await _sut.StopServerAsync(server);

        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }
}
