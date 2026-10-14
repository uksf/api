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

public class GameServerProcessManagerTests : GameServerProcessManagerTestBase
{
    [Fact]
    public void GetInstanceCount_ReturnsCountOfArmaProcesses()
    {
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, ""), new ProcessCommandLineInfo(2, "")]);

        _sut.GetInstanceCount().Should().Be(2);
    }

    [Fact]
    public void GetInstanceCount_WhenNoProcesses_ReturnsZero()
    {
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        _sut.GetInstanceCount().Should().Be(0);
    }

    [Fact]
    public async Task PushServerUpdateAsync_SendsReceiveServerUpdateWithInstanceCount()
    {
        var server = new DomainGameServer { Id = "s1", Name = "Test" };
        _servers.Add(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, "")]);

        await _sut.PushServerUpdateAsync(server);

        _mockRptLogService.Verify(x => x.GetLogSources(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1" && u.InstanceCount == 1)), Times.Once);
    }

    [Fact]
    public async Task PushServerUpdateAsync_SetsLogSourcesOnServer()
    {
        var server = new DomainGameServer { Id = "s1" };
        _servers.Add(server);
        var logSources = new List<RptLogSource> { new("server", true) };
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        _mockRptLogService.Setup(x => x.GetLogSources(server)).Returns(logSources);

        await _sut.PushServerUpdateAsync(server);

        server.LogSources.Should().BeEquivalentTo(logSources);
    }

    [Fact]
    public async Task PushAllServersUpdateAsync_SendsReceiveServersUpdateWithAllData()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1" } };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.PushAllServersUpdateAsync();

        _mockServersClient.Verify(x => x.ReceiveServersUpdate(It.Is<GameServersUpdate>(u => u.Servers.Count == 1 && u.InstanceCount == 0)), Times.Once);
    }

    [Fact]
    public async Task PushAllServersUpdateAsync_SetsLogSourcesOnAllServers()
    {
        var server1 = new DomainGameServer { Id = "s1" };
        _servers.Add(server1);
        var server2 = new DomainGameServer { Id = "s2" };
        _servers.Add(server2);
        var servers = new List<DomainGameServer> { server1, server2 };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        _mockRptLogService.Setup(x => x.GetLogSources(It.IsAny<DomainGameServer>())).Returns(new List<RptLogSource> { new("server", true) });

        await _sut.PushAllServersUpdateAsync();

        _mockRptLogService.Verify(x => x.GetLogSources(server1), Times.Once);
        _mockRptLogService.Verify(x => x.GetLogSources(server2), Times.Once);
        server1.LogSources.Should().HaveCount(1);
        server2.LogSources.Should().HaveCount(1);
    }

    [Fact]
    public async Task PushAllServersUpdateAsync_IncludesMissionsInUpdate()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1" } };
        var tempFile = Path.Combine(Path.GetTempPath(), "mission1.Altis.pbo");
        File.WriteAllBytes(tempFile, []);
        var missions = new List<MissionFile> { new(new FileInfo(tempFile)) };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns(missions);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.PushAllServersUpdateAsync();

        _mockServersClient.Verify(x => x.ReceiveServersUpdate(It.Is<GameServersUpdate>(u => u.Missions.Count == 1)), Times.Once);
    }
}
