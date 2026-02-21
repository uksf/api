using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerHelpersTests
{
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly GameServerHelpers _sut;

    public GameServerHelpersTests()
    {
        _sut = new GameServerHelpers(_mockVariablesService.Object, _mockProcessUtilities.Object, _mockLogger.Object);
    }

    private static DomainVariableItem CreateVariable(string key, object item)
    {
        return new DomainVariableItem { Key = key, Item = item };
    }

    private void SetupVariable(string key, string value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));
    }

    private static DomainGameServer CreateGameServer(
        GameEnvironment environment = GameEnvironment.Release,
        string profileName = "TestProfile",
        string name = "TestServer",
        string hostName = "Test Host",
        string password = "pass",
        string adminPassword = "adminpass",
        int port = 2302,
        int apiPort = 2303
    )
    {
        return new DomainGameServer
        {
            Environment = environment,
            ProfileName = profileName,
            Name = name,
            HostName = hostName,
            Password = password,
            AdminPassword = adminPassword,
            Port = port,
            ApiPort = apiPort,
            Mods = [],
            ServerMods = []
        };
    }

    [Fact]
    public void GetGameServerExecutablePath_Release_Uses_SERVER_PATH_RELEASE()
    {
        SetupVariable("SERVER_PATH_RELEASE", "C:/servers/release");

        var result = _sut.GetGameServerExecutablePath(CreateGameServer(GameEnvironment.Release));

        result.Should().Be(Path.Join("C:/servers/release", "arma3server_x64.exe"));
    }

    [Fact]
    public void GetGameServerExecutablePath_Rc_Uses_SERVER_PATH_RC()
    {
        SetupVariable("SERVER_PATH_RC", "C:/servers/rc");

        var result = _sut.GetGameServerExecutablePath(CreateGameServer(GameEnvironment.Rc));

        result.Should().Be(Path.Join("C:/servers/rc", "arma3server_x64.exe"));
    }

    [Fact]
    public void GetGameServerExecutablePath_Development_Uses_SERVER_PATH_DEV()
    {
        SetupVariable("SERVER_PATH_DEV", "C:/servers/dev");

        var result = _sut.GetGameServerExecutablePath(CreateGameServer(GameEnvironment.Development));

        result.Should().Be(Path.Join("C:/servers/dev", "arma3server_x64.exe"));
    }

    [Fact]
    public void GetGameServerExecutablePath_InvalidEnvironment_Throws()
    {
        var gameServer = CreateGameServer();
        gameServer.Environment = (GameEnvironment)99;

        var act = () => _sut.GetGameServerExecutablePath(gameServer);

        act.Should().Throw<ArgumentException>().WithMessage("Server environment is invalid");
    }

    [Fact]
    public void GetGameServerSettingsPath_Returns_Correct_Path()
    {
        SetupVariable("SERVER_PATH_RELEASE", "C:/servers/release");

        var result = _sut.GetGameServerSettingsPath();

        result.Should().Be(Path.Join("C:/servers/release", "userconfig", "cba_settings.sqf"));
    }

    [Fact]
    public void GetGameServerMissionsPath_Returns_MISSIONS_PATH()
    {
        SetupVariable("MISSIONS_PATH", "C:/missions");

        var result = _sut.GetGameServerMissionsPath();

        result.Should().Be("C:/missions");
    }

    [Fact]
    public void GetGameServerConfigPath_Uses_ProfileName_With_Cfg_Extension()
    {
        SetupVariable("SERVER_PATH_CONFIGS", "C:/configs");

        var result = _sut.GetGameServerConfigPath(CreateGameServer(profileName: "MyServer"));

        result.Should().Be(Path.Combine("C:/configs", "MyServer.cfg"));
    }

    [Fact]
    public void GetGameServerModsPaths_Returns_Correct_Path_With_Repo_Suffix()
    {
        SetupVariable("MODPACK_PATH_RELEASE", "C:/modpack/release");

        var result = _sut.GetGameServerModsPaths(GameEnvironment.Release);

        result.Should().Be(Path.Join("C:/modpack/release", "Repo"));
    }

    [Fact]
    public void GetGameServerModsPaths_InvalidEnvironment_Throws()
    {
        var act = () => _sut.GetGameServerModsPaths((GameEnvironment)99);

        act.Should().Throw<ArgumentException>().WithMessage("Server environment is invalid");
    }

    [Fact]
    public void FormatGameServerConfig_Substitutes_All_Fields()
    {
        SetupVariable("SERVER_COMMAND_PASSWORD", "cmdpass");
        var gameServer = CreateGameServer(hostName: "My Server", password: "secret", adminPassword: "admin123");

        var result = _sut.FormatGameServerConfig(gameServer, 40, "mission_name");

        result.Should().Contain("hostname = \"My Server\";");
        result.Should().Contain("password = \"secret\";");
        result.Should().Contain("passwordAdmin = \"admin123\";");
        result.Should().Contain("maxPlayers = 40;");
        result.Should().Contain("template = \"mission_name\";");
        result.Should().Contain("serverCommandPassword = \"cmdpass\";");
    }

    [Fact]
    public void FormatGameServerConfig_Strips_Pbo_From_MissionSelection()
    {
        SetupVariable("SERVER_COMMAND_PASSWORD", "cmdpass");
        var gameServer = CreateGameServer();

        var result = _sut.FormatGameServerConfig(gameServer, 40, "my_mission.pbo");

        result.Should().Contain("template = \"my_mission\";");
        result.Should().NotContain(".pbo");
    }

    [Fact]
    public void StripMilliseconds_Removes_Milliseconds()
    {
        var time = new TimeSpan(0, 2, 30, 45, 123);

        var result = _sut.StripMilliseconds(time);

        result.Milliseconds.Should().Be(0);
    }

    [Fact]
    public void StripMilliseconds_Preserves_Hours_Minutes_Seconds()
    {
        var time = new TimeSpan(0, 5, 42, 17, 999);

        var result = _sut.StripMilliseconds(time);

        result.Hours.Should().Be(5);
        result.Minutes.Should().Be(42);
        result.Seconds.Should().Be(17);
    }

    [Fact]
    public void GetArmaProcesses_Calls_GetProcesses_And_Returns_Filtered_Result()
    {
        _mockProcessUtilities.Setup(x => x.GetProcesses()).Returns([]);

        var result = _sut.GetArmaProcesses().ToList();

        result.Should().BeEmpty();
        _mockProcessUtilities.Verify(x => x.GetProcesses(), Times.Once);
    }

    [Fact]
    public void GetDlcModFoldersRegexString_Creates_Regex_From_Variable()
    {
        SetupVariable("SERVER_DLC_MOD_FOLDERS", "gm,vn,csla");

        var result = _sut.GetDlcModFoldersRegexString();

        result.Should().Contain("gm");
        result.Should().Contain("vn");
        result.Should().Contain("csla");
        result.Should().Contain("|");
    }

    [Fact]
    public void FormatGameServerLaunchArguments_Contains_Required_Parameters()
    {
        SetupVariable("SERVER_PATH_CONFIGS", "C:/configs");
        SetupVariable("SERVER_PATH_PROFILES", "C:/profiles");
        SetupVariable("SERVER_PATH_PERF", "C:/perf/perf.cfg");

        var gameServer = CreateGameServer(name: "Server1", port: 2302, apiPort: 2303);

        var result = _sut.FormatGameServerLaunchArguments(gameServer);

        result.Should().Contain("-config=");
        result.Should().Contain("-profiles=");
        result.Should().Contain("-cfg=");
        result.Should().Contain("-name=Server1");
        result.Should().Contain("-port=2302");
        result.Should().Contain("-apiport=\"2303\"");
        result.Should().Contain("-bandwidthAlg=2");
        result.Should().Contain("-filePatching");
        result.Should().Contain("-limitFPS=200");
    }

    [Fact]
    public void FormatHeadlessClientLaunchArguments_Contains_Required_Parameters()
    {
        SetupVariable("SERVER_PATH_PROFILES", "C:/profiles");
        SetupVariable("SERVER_HEADLESS_NAMES", "HC1,HC2,HC3");

        var gameServer = CreateGameServer(name: "Server1", port: 2302, apiPort: 2303, password: "secret");

        var result = _sut.FormatHeadlessClientLaunchArguments(gameServer, 0);

        result.Should().Contain("-name=HC1");
        result.Should().Contain("-port=2302");
        result.Should().Contain("-apiport=\"2304\"");
        result.Should().Contain("-password=secret");
        result.Should().Contain("-localhost=127.0.0.1");
        result.Should().Contain("-connect=localhost");
        result.Should().Contain("-client");
        result.Should().Contain("-filePatching");
        result.Should().Contain("-limitFPS=200");
    }
}
