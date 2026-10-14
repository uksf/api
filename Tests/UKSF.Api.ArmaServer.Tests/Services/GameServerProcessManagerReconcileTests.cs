using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerReconcileTests : GameServerProcessManagerTestBase
{
    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoProcesses_ResetsAllServers()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                ProcessId = 1234,
                HeadlessClients = [],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        var result = await _sut.GetAllServerStatusesAsync();

        result[0].Status.Running.Should().BeFalse();
        result[0].ProcessId.Should().BeNull();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenSkipFeatureEnabled_ReturnsServersUnchangedWithoutWriting()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1", Status = new GameServerStatus { Running = true } } };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(true);

        var result = await _sut.GetAllServerStatusesAsync();

        result.Should().HaveCount(1);
        result[0].Status.Running.Should().BeTrue();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
        _mockHelpers.Verify(x => x.GetGameServerArmaProcesses(), Times.Never);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoProcesses_FinalisesActiveSessions()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                HeadlessClients = [],
                Status = new GameServerStatus { CurrentMissionSessionId = "session-1" }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.GetAllServerStatusesAsync();

        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("session-1"), Times.Once);
        _mockOpSessionCaptureService.Verify(x => x.CaptureEndedAsync("session-1"), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoProcesses_ClearsHeadlessClients()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                HeadlessClients = [new(5001, HeadlessClientStart), new(5002, HeadlessClientStart)],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.GetAllServerStatusesAsync();

        servers[0].HeadlessClients.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenOneServerReconcileThrows_StillProcessesOthersAndDoesNotThrow()
    {
        var serverA = new DomainGameServer
        {
            Id = "a",
            Name = "A",
            ApiPort = 2303,
            Port = 2302,
            ProcessId = 1234,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-a" }
        };
        _servers.Add(serverA);
        var serverB = new DomainGameServer
        {
            Id = "b",
            Name = "B",
            ApiPort = 2313,
            Port = 2312,
            ProcessId = 5678,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-b" }
        };
        _servers.Add(serverB);
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { serverA, serverB });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, "")]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockContext.Setup(x => x.Replace(serverA)).ThrowsAsync(new Exception("boom"));

        var act = async () => await _sut.GetAllServerStatusesAsync();

        await act.Should().NotThrowAsync();
        _mockContext.Verify(x => x.Replace(serverB), Times.Once);
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("sess-b"), Times.Once);
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoArmaProcesses_OneResetThrows_StillResetsOthers()
    {
        var serverA = new DomainGameServer
        {
            Id = "a",
            Name = "A",
            ProcessId = 1,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-a" }
        };
        _servers.Add(serverA);
        var serverB = new DomainGameServer
        {
            Id = "b",
            Name = "B",
            ProcessId = 2,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-b" }
        };
        _servers.Add(serverB);
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { serverA, serverB });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockContext.Setup(x => x.Replace(serverA)).ThrowsAsync(new Exception("boom"));

        var act = async () => await _sut.GetAllServerStatusesAsync();

        await act.Should().NotThrowAsync();
        _mockContext.Verify(x => x.Replace(serverB), Times.Once);
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("sess-b"), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WithProcesses_QueriesStatusEndpoint()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            HeadlessClients = [],
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Main.cfg -port=2302 ")]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(5678)).Returns(Process.GetCurrentProcess());

        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _sut.GetAllServerStatusesAsync();

        result.Should().HaveCount(1);
        result[0].ProcessId.Should().Be(5678);
        mockHandler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_ScansArmaProcessesOnlyOncePerCall()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            HeadlessClients = [],
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Main.cfg -port=2302 ")]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(5678)).Returns(Process.GetCurrentProcess());
        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.RequestTimeout));
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _sut.GetAllServerStatusesAsync();

        _mockHelpers.Verify(x => x.GetGameServerArmaProcesses(), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WithProcesses_NoMatchingProcess_ResetsServer()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            HeadlessClients = [],
            Status = new GameServerStatus { Running = true }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Other.cfg -port=2310 ")]);

        var result = await _sut.GetAllServerStatusesAsync();

        result[0].Status.Running.Should().BeFalse();
        result[0].ProcessId.Should().BeNull();
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenMainProcessGoneButHeadlessClientAlive_KillsAndClearsHeadlessClient()
    {
        var server = new DomainGameServer
        {
            Id = "leak-1",
            Name = "Test",
            ApiPort = 2303,
            Port = 2302,
            ProcessId = 1234,
            HeadlessClients = [],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
        };
        _servers.Add(server);
        var hcProcess = new ProcessCommandLineInfo(5001, "-port=2302 -client");
        _mockContext.Setup(x => x.Get()).Returns([server]);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([hcProcess]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);

        await _sut.GetAllServerStatusesAsync();

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        server.HeadlessClients.Should().BeEmpty();
        server.ProcessId.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("sess-1"), Times.Once);
    }
}
