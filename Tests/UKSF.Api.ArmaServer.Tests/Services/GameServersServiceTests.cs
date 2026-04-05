using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServersServiceTests
{
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IGameServersContext> _mockGameServersContext = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IVariablesContext> _mockVariablesContext = new();
    private readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    private readonly GameServersService _subject;

    public GameServersServiceTests()
    {
        var mockClients = new Mock<IServersClient>();
        var mockHubClients = new Mock<IHubClients<IServersClient>>();
        mockHubClients.Setup(x => x.All).Returns(mockClients.Object);
        _mockServersHub.Setup(x => x.Clients).Returns(mockHubClients.Object);

        _subject = new GameServersService(
            _mockGameServersContext.Object,
            _mockGameServerHelpers.Object,
            _mockVariablesService.Object,
            _mockVariablesContext.Object,
            _mockServersHub.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void GetServers_ShouldReturnAllServers()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1", Name = "Server 1" }, new() { Id = "s2", Name = "Server 2" } };
        _mockGameServersContext.Setup(x => x.Get()).Returns(servers);

        var result = _subject.GetServers();

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(servers);
    }

    [Fact]
    public void GetServer_ShouldReturnSingleServer()
    {
        var server = new DomainGameServer { Id = "s1", Name = "Server 1" };
        _mockGameServersContext.Setup(x => x.GetSingle("s1")).Returns(server);

        var result = _subject.GetServer("s1");

        result.Should().Be(server);
    }

    [Fact]
    public async Task AddServerAsync_ShouldSetOrderAndAdd()
    {
        var existingServers = new List<DomainGameServer> { new() { Id = "s1", Order = 0 }, new() { Id = "s2", Order = 1 } };
        _mockGameServersContext.Setup(x => x.Get()).Returns(existingServers);

        var newServer = new DomainGameServer { Id = "s3", Name = "New Server" };

        await _subject.AddServerAsync(newServer);

        newServer.Order.Should().Be(2);
        _mockGameServersContext.Verify(x => x.Add(newServer), Times.Once);
    }

    [Fact]
    public async Task DeleteServerAsync_ShouldDeleteServer()
    {
        var server = new DomainGameServer { Id = "s1", Name = "Server 1" };
        _mockGameServersContext.Setup(x => x.GetSingle("s1")).Returns(server);

        await _subject.DeleteServerAsync("s1");

        _mockGameServersContext.Verify(x => x.Delete("s1"), Times.Once);
    }

    [Fact]
    public void GetDisabledState_ShouldReturnVariableValue()
    {
        var variable = new DomainVariableItem { Key = "SERVER_CONTROL_DISABLED", Item = "true" };
        _mockVariablesService.Setup(x => x.GetVariable("SERVER_CONTROL_DISABLED")).Returns(variable);

        var result = _subject.GetDisabledState();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetDisabledStateAsync_ShouldUpdateVariableAndPushToHub()
    {
        await _subject.SetDisabledStateAsync(true);

        _mockVariablesContext.Verify(x => x.Update("SERVER_CONTROL_DISABLED", true), Times.Once);
        _mockServersHub.Verify(x => x.Clients.All.ReceiveDisabledState(true), Times.Once);
    }
}

internal class MockHttpMessageHandler(System.Net.HttpStatusCode statusCode) : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    public string LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri?.ToString();
        return Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
