using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerHelpersProcessTests
{
    private const string Owned = "OWNED";

    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly GameServerHelpers _sut;

    public GameServerHelpersProcessTests()
    {
        var configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string> { ["Kestrel:Endpoints:Http:Url"] = "http://localhost:5500" })
                            .Build();
        _sut = new GameServerHelpers(new Mock<IVariablesService>().Object, _mockProcessUtilities.Object, new Mock<IUksfLogger>().Object, configuration);
    }

    private static ProcessCommandLineInfo Process(int pid, string commandLine) => new(pid, commandLine.Replace(Owned, "-apiUrl=\"http://127.0.0.1:5500\""));

    private void Running(params ProcessCommandLineInfo[] processes) =>
        _mockProcessUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(processes);

    [Theory]
    [InlineData("-profiles=C:/profiles/GameDataExport -port=3302", true)]
    [InlineData(@"-profiles=C:\profiles\GameDataExport -port=3302", true)]
    [InlineData("-profiles=C:/profiles/GameDataExport", true)]
    [InlineData("-profiles=C:/profiles/GameDataExport -client", true)]
    [InlineData("-profiles=C:/profiles/GameDataExportSomethingElse -port=3302", false)]
    [InlineData("-profiles=C:/profiles/MainServer -port=2302", false)]
    [InlineData("-profiles=C:/profiles/NotGameDataExport -port=2302", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("\"arma3server_x64.exe\" -profiles=\"C:/profiles/GameDataExport\" -port=3302", true)]
    [InlineData("\"arma3server_x64.exe\" -profiles=\"C:/profiles/GameDataExportSomethingElse\" -port=3302", false)]
    public void IsGameDataExportProcess_Matches_Only_GameDataExport_Profile_Leaf(string commandLine, bool expected)
    {
        var result = _sut.IsGameDataExportProcess(commandLine);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetGameServerArmaProcesses_Excludes_GameDataExport_Processes()
    {
        var gameServerProcess = Process(1001, "-profiles=C:/profiles/MainServer -port=2302 OWNED");
        var exportProcess = Process(1002, "-profiles=C:/profiles/GameDataExport -port=3302 OWNED");
        _mockProcessUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns([gameServerProcess, exportProcess]);

        var result = _sut.GetGameServerArmaProcesses();

        result.Should().ContainSingle();
        result.Single().ProcessId.Should().Be(1001);
    }

    [Fact]
    public void GetGameServerArmaProcesses_Returns_All_When_No_GameDataExport_Processes()
    {
        var process1 = Process(1001, "-profiles=C:/profiles/MainServer -port=2302 OWNED");
        var process2 = Process(1002, "-profiles=C:/profiles/AnotherServer -port=2402 OWNED");
        _mockProcessUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns([process1, process2]);

        var result = _sut.GetGameServerArmaProcesses();

        result.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(@"-profiles=""C:/profs/GameDataExport"" -port=3302", true)]
    [InlineData(@"-profiles=""C:/profs/DevRun_abcd1234"" -port=3304", true)]
    [InlineData(@"-profiles=""C:/profs/uksf"" -port=2302", false)]
    [InlineData("", false)]
    public void IsSyntheticProcess_matches_config_export_and_devrun_profile_names(string commandLine, bool expected)
    {
        _sut.IsSyntheticProcess(commandLine).Should().Be(expected);
    }

    [Fact]
    public void GetGameServerArmaProcesses_Excludes_Both_GameDataExport_And_DevRun_Processes()
    {
        var normalProcess = Process(1001, "-profiles=C:/profiles/uksf -port=2302 OWNED");
        var gameDataExportProcess = Process(1002, "-profiles=C:/profiles/GameDataExport -port=3302 OWNED");
        var devRunProcess = Process(1003, "-profiles=C:/profiles/DevRun_abcd1234 -port=3304 OWNED");
        _mockProcessUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns([normalProcess, gameDataExportProcess, devRunProcess]);

        var result = _sut.GetGameServerArmaProcesses();

        result.Should().ContainSingle();
        result.Single().ProcessId.Should().Be(1001);
    }

    [Fact]
    public void GetGameServerArmaProcesses_ExcludesProcessesLaunchedByAnotherApi()
    {
        Running(
            Process(1001, "-profiles=C:/profiles/uksf -port=2302 OWNED"),
            Process(1002, "-profiles=C:/profiles/uksf -port=2402 -apiUrl=\"http://127.0.0.1:5600\""),
            Process(1003, "-profiles=C:/profiles/uksf -port=2502 -apiUrl=\"http://127.0.0.1:55001\""),
            Process(1004, "-profiles=C:/profiles/uksf -port=2602")
        );

        _sut.GetGameServerArmaProcesses().Select(x => x.ProcessId).Should().Equal(1001);
    }

    [Theory]
    [InlineData("-name=HC1 -apiUrl=\"http://127.0.0.1:5500\" -client", true)]
    [InlineData("-name=HC1 -apiUrl=http://127.0.0.1:5500 -client", true)]
    [InlineData("-name=HC1 -apiUrl=http://127.0.0.1:5500", true)]
    [InlineData("-name=HC1 -APIURL=\"HTTP://127.0.0.1:5500\"", true)]
    [InlineData("-name=HC1 -apiUrl=\"http://127.0.0.1:5500/\"", false)]
    [InlineData("-name=HC1 -apiUrl=\"http://127.0.0.1:55001\"", false)]
    [InlineData("-name=HC1 -apiUrl=\"http://localhost:5500\" -client", true)]
    [InlineData("-apiUrl=\"http://127.0.0.1:5600\" -password=x-apiUrl=http://127.0.0.1:5500 -client", false)]
    [InlineData("-apiUrl=\"http://127.0.0.1:5500\" -apiUrl=\"http://127.0.0.1:5600\"", false)]
    [InlineData("-name=HC1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOwnedProcess_MatchesOnlyThisApisUrl(string commandLine, bool expected)
    {
        _sut.IsOwnedProcess(commandLine).Should().Be(expected);
    }

    [Fact]
    public void IsOwnedProcessId_IsFalseWhenTheStoredPidNowBelongsToAnotherApi()
    {
        Running(Process(1001, "-port=2302 OWNED"), Process(1002, "-port=2402 -apiUrl=\"http://127.0.0.1:5600\""));

        _sut.IsOwnedProcessId(1001).Should().BeTrue();
        _sut.IsOwnedProcessId(1002).Should().BeFalse();
        _sut.IsOwnedProcessId(1003).Should().BeFalse();
    }

    [Fact]
    public void IsOwnedApiPort_MatchesOnlyAnOwnedProcessLaunchedWithThatApiPort()
    {
        Running(Process(1001, "-port=2302 -apiport=\"2303\" OWNED"), Process(1002, "-port=2402 -apiport=\"2403\" -apiUrl=\"http://127.0.0.1:5600\""));

        _sut.IsOwnedApiPort(2303).Should().BeTrue();
        _sut.IsOwnedApiPort(2403).Should().BeFalse();
        _sut.IsOwnedApiPort(230).Should().BeFalse();
    }
}
