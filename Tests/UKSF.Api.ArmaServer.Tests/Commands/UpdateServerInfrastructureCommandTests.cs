using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Commands;

public class UpdateServerInfrastructureCommandTests
{
    private readonly Mock<ISteamCmdService> _steamCmdService = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IGameServerHelpers> _gameServerHelpers = new();
    private readonly UpdateServerInfrastructureCommand _subject;

    public UpdateServerInfrastructureCommandTests()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_INFRA_UPDATING")).Returns(new DomainVariableItem { Key = "SERVER_INFRA_UPDATING", Item = false });
        _subject = new UpdateServerInfrastructureCommand(
            _steamCmdService.Object,
            new Mock<IVariablesContext>().Object,
            _variablesService.Object,
            _gameServerHelpers.Object,
            new Mock<IHubContext<ServersHub, IServersClient>>().Object,
            new Mock<IUksfLogger>().Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnotherEnvironmentsServerIsRunning_RefusesToOverwriteSharedBinaries()
    {
        _gameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine())
                          .Returns([new ProcessCommandLineInfo(1001, "-port=2302 -apiUrl=\"http://127.0.0.1:5600\"")]);

        var act = () => _subject.ExecuteAsync();

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*servers running*");
        _steamCmdService.Verify(x => x.UpdateServer(), Times.Never);
    }
}
