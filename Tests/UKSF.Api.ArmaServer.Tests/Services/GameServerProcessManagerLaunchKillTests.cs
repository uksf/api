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

public class GameServerProcessManagerLaunchKillTests : GameServerProcessManagerTestBase
{
    [Fact]
    public async Task KillServerAsync_ClearsStateAndPersistsAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            LaunchedBy = "user1",
            HeadlessClients = [],
            Status = new GameServerStatus { Running = true }
        };
        _servers.Add(server);
        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.KillServerAsync(server);

        server.ProcessId.Should().BeNull();
        server.LaunchedBy.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1")), Times.Once);
    }

    [Fact]
    public async Task KillServerAsync_KillsHeadlessClients()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClients = [new(5001, HeadlessClientStart), new(5002, HeadlessClientStart)],
            Status = new GameServerStatus { Running = true }
        };
        _servers.Add(server);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.KillServerAsync(server);

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(5002), Times.Once);
        server.HeadlessClients.Should().BeEmpty();
    }

    [Fact]
    public async Task KillServerAsync_FinalisesActiveSession()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = null,
            HeadlessClients = [],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "session-1" }
        };
        _servers.Add(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.KillServerAsync(server);

        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("session-1"), Times.Once);
        _mockOpSessionCaptureService.Verify(x => x.CaptureEndedAsync("session-1"), Times.Once);
    }

    [Fact]
    public async Task KillAllAsync_ClearsAllServersAndReturnsKillCount()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                ProcessId = 1234,
                HeadlessClients = [],
                Status = new GameServerStatus { Running = true }
            },
            new()
            {
                Id = "s2",
                ProcessId = 5678,
                HeadlessClients = [],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        var killed = await _sut.KillAllAsync();

        killed.Should().Be(0);
        servers.Should()
               .AllSatisfy(s =>
                   {
                       s.ProcessId.Should().BeNull();
                       s.Status.Running.Should().BeFalse();
                   }
               );
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Exactly(2));
    }

    [Fact]
    public async Task KillAllAsync_WhenOneProcessThrowsOnKill_StillKillsOthersAndCleansUpAllServers()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                ProcessId = 1234,
                HeadlessClients = [],
                Status = new GameServerStatus { Running = true }
            },
            new()
            {
                Id = "s2",
                ProcessId = 5678,
                HeadlessClients = [],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1234, ""), new ProcessCommandLineInfo(5678, "")]);
        var process1 = Process.GetCurrentProcess();
        var process2 = Process.GetCurrentProcess();
        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns(process1);
        _mockProcessUtilities.Setup(x => x.FindProcessById(5678)).Returns(process2);
        _mockProcessUtilities.Setup(x => x.KillProcess(process1, It.IsAny<bool>())).Throws<Win32Exception>();

        var killed = await _sut.KillAllAsync();

        killed.Should().Be(2);
        _mockProcessUtilities.Verify(x => x.KillProcess(process2, It.IsAny<bool>()), Times.Once);
        servers.Should()
               .AllSatisfy(s =>
                   {
                       s.ProcessId.Should().BeNull();
                       s.Status.Running.Should().BeFalse();
                   }
               );
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Exactly(2));
    }

    [Fact]
    public async Task LaunchServerAsync_LaunchesProcessAndHCsAndPersists()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            NumberHeadlessClients = 1,
            HeadlessClients = [],
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockHelpers.Setup(x => x.GetGameServerExecutablePath(server)).Returns("arma3server_x64.exe");
        _mockHelpers.Setup(x => x.FormatGameServerLaunchArguments(server)).Returns("-port=2302");
        _mockHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(server, 0)).Returns("-port=2302 -client");
        _mockHelpers.Setup(x => x.GetGameServerConfigPath(server)).Returns(Path.Combine(Path.GetTempPath(), "test_config.cfg"));
        _mockHelpers.Setup(x => x.FormatGameServerConfig(server, 40, "mission.Altis.pbo")).Returns("config content");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302")).Returns(1234);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302 -client")).Returns(5001);
        _mockProcessUtilities.Setup(x => x.FindProcessById(5001)).Returns(Process.GetCurrentProcess());
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, ""), new ProcessCommandLineInfo(2, "")]);

        await _sut.LaunchServerAsync(server, "mission.Altis.pbo", "user1", 40);

        server.ProcessId.Should().Be(1234);
        server.Status.Launching.Should().BeTrue();
        server.Status.Mission.Should().Be("mission");
        server.Status.Map.Should().Be("Altis");
        server.LaunchedBy.Should().Be("user1");
        server.HeadlessClients.Select(x => x.ProcessId).Should().Contain(5001);
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task LaunchServerAsync_ClearsStaleHeadlessClientsBeforeRelaunch()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            NumberHeadlessClients = 1,
            HeadlessClients = [new(9999, HeadlessClientStart)],
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockHelpers.Setup(x => x.GetGameServerExecutablePath(server)).Returns("arma3server_x64.exe");
        _mockHelpers.Setup(x => x.FormatGameServerLaunchArguments(server)).Returns("-port=2302");
        _mockHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(server, 0)).Returns("-port=2302 -client");
        _mockHelpers.Setup(x => x.GetGameServerConfigPath(server)).Returns(Path.Combine(Path.GetTempPath(), "test_config.cfg"));
        _mockHelpers.Setup(x => x.FormatGameServerConfig(server, 40, "mission.Altis.pbo")).Returns("config content");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302")).Returns(1234);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302 -client")).Returns(5001);
        _mockProcessUtilities.Setup(x => x.FindProcessById(5001)).Returns(Process.GetCurrentProcess());
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.LaunchServerAsync(server, "mission.Altis.pbo", "user1", 40);

        server.HeadlessClients.Select(x => x.ProcessId).Should().BeEquivalentTo([5001]);
    }

    [Fact]
    public async Task LaunchServerAsync_WhenMissionNameHasNoPboExtension_StillParsesMap()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            NumberHeadlessClients = 0,
            HeadlessClients = [],
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockHelpers.Setup(x => x.GetGameServerExecutablePath(server)).Returns("arma3server_x64.exe");
        _mockHelpers.Setup(x => x.FormatGameServerLaunchArguments(server)).Returns("-port=2302");
        _mockHelpers.Setup(x => x.GetGameServerConfigPath(server)).Returns(Path.Combine(Path.GetTempPath(), "test_config.cfg"));
        _mockHelpers.Setup(x => x.FormatGameServerConfig(server, 40, "mission.Altis")).Returns("config content");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302")).Returns(1234);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.LaunchServerAsync(server, "mission.Altis", "user1", 40);

        server.Status.Mission.Should().Be("mission");
        server.Status.Map.Should().Be("Altis");
    }
}
