using System.Net.Http;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Tests.Services;

public abstract class GameServerProcessManagerTestBase
{
    protected readonly Mock<IGameServersContext> _mockContext = new();
    protected readonly Mock<IGameServerHelpers> _mockHelpers = new();
    protected readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    protected readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    protected readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    protected readonly Mock<IMissionsService> _mockMissionsService = new();
    protected readonly Mock<IRptLogService> _mockRptLogService = new();
    protected readonly Mock<IMissionStatsService> _mockMissionStatsService = new();
    protected readonly Mock<IOpSessionCaptureService> _mockOpSessionCaptureService = new();
    protected readonly Mock<IVariablesService> _mockVariablesService = new();
    protected readonly Mock<IUksfLogger> _mockLogger = new();
    protected readonly Mock<IServersClient> _mockServersClient;
    protected readonly GameServerProcessManager _sut;

    protected GameServerProcessManagerTestBase()
    {
        var mockClients = new Mock<IHubClients<IServersClient>>();
        _mockServersClient = new Mock<IServersClient>();
        mockClients.Setup(x => x.All).Returns(_mockServersClient.Object);
        _mockServersHub.Setup(x => x.Clients).Returns(mockClients.Object);

        _sut = new GameServerProcessManager(
            _mockContext.Object,
            _mockHelpers.Object,
            _mockProcessUtilities.Object,
            _mockHttpClientFactory.Object,
            _mockServersHub.Object,
            _mockMissionsService.Object,
            _mockRptLogService.Object,
            _mockMissionStatsService.Object,
            _mockOpSessionCaptureService.Object,
            _mockVariablesService.Object,
            _mockLogger.Object
        );
    }
}
