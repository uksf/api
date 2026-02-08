using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Services;
using UKSF.Api.Integrations.Teamspeak.Signalr.Clients;
using UKSF.Api.Integrations.Teamspeak.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakManagerServiceTests
{
    private readonly Mock<IHubContext<TeamspeakHub, ITeamspeakClient>> _mockHub;
    private readonly Mock<ITeamspeakClient> _mockClients;
    private readonly Mock<IProcessUtilities> _mockProcessUtilities;
    private readonly Mock<IVariablesService> _mockVariablesService;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly TeamspeakManagerService _subject;

    public TeamspeakManagerServiceTests()
    {
        _mockHub = new Mock<IHubContext<TeamspeakHub, ITeamspeakClient>>();
        _mockClients = new Mock<ITeamspeakClient>();
        _mockHub.Setup(x => x.Clients.All).Returns(_mockClients.Object);
        _mockProcessUtilities = new Mock<IProcessUtilities>();
        _mockVariablesService = new Mock<IVariablesService>();
        _mockLogger = new Mock<IUksfLogger>();

        _subject = new TeamspeakManagerService(_mockHub.Object, _mockProcessUtilities.Object, _mockVariablesService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Start_ShouldDoNothing_WhenTeamspeakDisabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(false);

        _subject.Start();

        _mockVariablesService.Verify(x => x.GetFeatureState("TEAMSPEAK"), Times.Once);
    }

    [Fact]
    public void Stop_ShouldNotThrow_WhenNotStarted()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);

        var act = () => _subject.Stop();

        act.Should().NotThrow();
    }

    [Fact]
    public void Stop_ShouldDoNothing_WhenTeamspeakDisabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(false);

        var act = () => _subject.Stop();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SendGroupProcedure_ShouldDoNothing_WhenTeamspeakDisabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(false);

        await _subject.SendGroupProcedure(TeamspeakProcedureType.Groups, new TeamspeakGroupProcedure());

        _mockClients.Verify(x => x.Receive(It.IsAny<TeamspeakProcedureType>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task SendGroupProcedure_ShouldCallHub_WhenTeamspeakEnabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);
        var procedure = new TeamspeakGroupProcedure { ClientDbId = 1, ServerGroup = 2 };

        await _subject.SendGroupProcedure(TeamspeakProcedureType.Assign, procedure);

        _mockClients.Verify(x => x.Receive(TeamspeakProcedureType.Assign, procedure), Times.Once);
    }

    [Fact]
    public async Task SendProcedure_ShouldDoNothing_WhenTeamspeakDisabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(false);

        await _subject.SendProcedure(TeamspeakProcedureType.Shutdown, new { });

        _mockClients.Verify(x => x.Receive(It.IsAny<TeamspeakProcedureType>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task SendProcedure_ShouldCallHub_WhenTeamspeakEnabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);
        var args = new { message = "test" };

        await _subject.SendProcedure(TeamspeakProcedureType.Message, args);

        _mockClients.Verify(x => x.Receive(TeamspeakProcedureType.Message, args), Times.Once);
    }

    [Fact]
    public void Start_ThenStop_ShouldNotThrow()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_SERVER_RUN")).Returns(new DomainVariableItem { Key = "TEAMSPEAK_SERVER_RUN", Item = false });
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_RUN")).Returns(new DomainVariableItem { Key = "TEAMSPEAK_RUN", Item = false });

        _subject.Start();

        var act = () => _subject.Stop();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task KeepOnline_ShouldLogErrorAndContinue_WhenIterationFails()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_SERVER_RUN")).Throws(new InvalidOperationException("variable error"));

        _subject.Start();

        // Wait for at least one iteration to fail and be caught
        await Task.Delay(TimeSpan.FromSeconds(3));

        _subject.Stop();

        // The key assertion: error was logged, not thrown (no process crash)
        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("KeepOnline")), It.IsAny<Exception>()), Times.AtLeastOnce);
    }
}
