using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class RptLogServiceTests : IDisposable
{
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly RptLogService _sut;
    private readonly List<string> _tempDirectories = [];

    public RptLogServiceTests()
    {
        _sut = new RptLogService(_mockVariablesService.Object);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    private static DomainVariableItem CreateVariable(string key, object item)
    {
        return new DomainVariableItem { Key = key, Item = item };
    }

    private void SetupVariable(string key, string value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));
    }

    private static DomainGameServer CreateGameServer(string name = "Main", int numberHeadlessClients = 0)
    {
        return new DomainGameServer
        {
            Name = name,
            NumberHeadlessClients = numberHeadlessClients,
            Environment = GameEnvironment.Release,
            Mods = [],
            ServerMods = []
        };
    }

    private string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"RptLogServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        _tempDirectories.Add(dir);
        return dir;
    }

    #region GetLogSources

    [Fact]
    public void GetLogSources_ReturnsServerOnly_WhenNoHeadlessClients()
    {
        var server = CreateGameServer(numberHeadlessClients: 0);

        var result = _sut.GetLogSources(server);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Server");
        result[0].IsServer.Should().BeTrue();
    }

    [Fact]
    public void GetLogSources_ReturnsServerAndHcs_WhenHeadlessClientsConfigured()
    {
        SetupVariable("SERVER_HEADLESS_NAMES", "Jarvis,Ultron,Vision");
        var server = CreateGameServer(numberHeadlessClients: 2);

        var result = _sut.GetLogSources(server);

        result.Should().HaveCount(3);
        result[0].Should().Be(new RptLogSource("Server", true));
        result[1].Should().Be(new RptLogSource("Jarvis", false));
        result[2].Should().Be(new RptLogSource("Ultron", false));
    }

    [Fact]
    public void GetLogSources_ReturnsCorrectHcNames_ForThreeHeadlessClients()
    {
        SetupVariable("SERVER_HEADLESS_NAMES", "Jarvis,Ultron,Vision");
        var server = CreateGameServer(numberHeadlessClients: 3);

        var result = _sut.GetLogSources(server);

        result.Should().HaveCount(4);
        result[0].Should().Be(new RptLogSource("Server", true));
        result[1].Should().Be(new RptLogSource("Jarvis", false));
        result[2].Should().Be(new RptLogSource("Ultron", false));
        result[3].Should().Be(new RptLogSource("Vision", false));
    }

    #endregion

    #region GetLatestRptFilePath

    [Fact]
    public void GetLatestRptFilePath_ReturnsLatestFile_ForServer()
    {
        var tempDir = CreateTempDirectory();
        var serverProfileDir = Path.Combine(tempDir, "Main");
        Directory.CreateDirectory(serverProfileDir);

        File.WriteAllText(Path.Combine(serverProfileDir, "arma3server_x64_2024-01-15_18-00-00.rpt"), "old");
        File.WriteAllText(Path.Combine(serverProfileDir, "arma3server_x64_2024-01-15_20-30-00.rpt"), "new");

        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main");

        var result = _sut.GetLatestRptFilePath(server, "Server");

        result.Should().Be(Path.Combine(serverProfileDir, "arma3server_x64_2024-01-15_20-30-00.rpt"));
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsLatestFile_ForHc()
    {
        var tempDir = CreateTempDirectory();
        var hcProfileDir = Path.Combine(tempDir, "MainJarvis");
        Directory.CreateDirectory(hcProfileDir);

        File.WriteAllText(Path.Combine(hcProfileDir, "arma3server_x64_2024-01-15_18-00-00.rpt"), "old");
        File.WriteAllText(Path.Combine(hcProfileDir, "arma3server_x64_2024-01-15_20-30-00.rpt"), "new");

        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main");

        var result = _sut.GetLatestRptFilePath(server, "Jarvis");

        result.Should().Be(Path.Combine(hcProfileDir, "arma3server_x64_2024-01-15_20-30-00.rpt"));
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsNull_WhenNoFilesExist()
    {
        var tempDir = CreateTempDirectory();
        var serverProfileDir = Path.Combine(tempDir, "Main");
        Directory.CreateDirectory(serverProfileDir);

        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main");

        var result = _sut.GetLatestRptFilePath(server, "Server");

        result.Should().BeNull();
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsNull_WhenDirectoryNotFound()
    {
        var tempDir = CreateTempDirectory();
        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "NonExistent");

        var result = _sut.GetLatestRptFilePath(server, "Server");

        result.Should().BeNull();
    }

    #endregion
}
