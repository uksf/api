using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class GameServersControllerRptTests : IDisposable
{
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IRptLogService> _mockRptLogService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly GameServersController _sut;
    private readonly List<string> _tempFiles = [];

    public GameServersControllerRptTests()
    {
        _sut = new GameServersController(
            _mockGameServersService.Object,
            Mock.Of<IGameServerProcessManager>(),
            Mock.Of<IMissionsService>(),
            _mockRptLogService.Object,
            Mock.Of<IGameServerHelpers>(),
            _mockLogger.Object,
            Mock.Of<IHttpContextService>()
        );

        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private static DomainGameServer CreateGameServer(string id = "server1", string name = "Main")
    {
        return new DomainGameServer
        {
            Id = id,
            Name = name,
            Environment = GameEnvironment.Release,
            Mods = [],
            ServerMods = []
        };
    }

    [Fact]
    public void GetGameServers_PopulatesLogSourcesOnEachServer()
    {
        var server1 = CreateGameServer("server1", "Main");
        server1.NumberHeadlessClients = 1;
        var server2 = CreateGameServer("server2", "Training");
        server2.NumberHeadlessClients = 0;

        _mockGameServersService.Setup(x => x.GetServers()).Returns(new List<DomainGameServer> { server1, server2 });

        var sources1 = new List<RptLogSource> { new("Server", true), new("HC0", false) };
        var sources2 = new List<RptLogSource> { new("Server", true) };
        _mockRptLogService.Setup(x => x.GetLogSources(server1)).Returns(sources1);
        _mockRptLogService.Setup(x => x.GetLogSources(server2)).Returns(sources2);

        var result = _sut.GetGameServers();

        var servers = result.Servers.ToList();
        servers[0].LogSources.Should().BeEquivalentTo(sources1);
        servers[1].LogSources.Should().BeEquivalentTo(sources2);
    }

    [Fact]
    public void DownloadLog_ReturnsFileStream()
    {
        var server = CreateGameServer();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.rpt");
        File.WriteAllText(tempFile, "log content here");
        _tempFiles.Add(tempFile);

        _mockGameServersService.Setup(x => x.GetServer("server1")).Returns(server);
        _mockRptLogService.Setup(x => x.GetLatestRptFilePath(server, "Server")).Returns(tempFile);

        var result = _sut.DownloadLog("server1", "Server");

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("text/plain");
        fileResult.FileDownloadName.Should().Be(Path.GetFileName(tempFile));
        fileResult.FileStream.Should().NotBeNull();
        fileResult.FileStream.Dispose();
    }

    [Fact]
    public void DownloadLog_ReturnsNotFound_WhenNoLogFile()
    {
        var server = CreateGameServer();

        _mockGameServersService.Setup(x => x.GetServer("server1")).Returns(server);
        _mockRptLogService.Setup(x => x.GetLatestRptFilePath(server, "Server")).Returns((string)null);

        var result = _sut.DownloadLog("server1", "Server");

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be("No log file found");
    }
}
