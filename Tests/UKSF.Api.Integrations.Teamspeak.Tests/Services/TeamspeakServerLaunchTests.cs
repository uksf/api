using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Query;
using UKSF.Api.Integrations.Teamspeak.Services;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakServerLaunchTests
{
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly TaskCompletionSource<string> _launched = new();
    private readonly TeamspeakManagerService _subject;

    public TeamspeakServerLaunchTests()
    {
        var mockQuery = new Mock<ITeamspeakQuery>();
        mockQuery.Setup(x => x.GetClients()).ReturnsAsync([]);
        mockQuery.Setup(x => x.GetChannelNames()).ReturnsAsync(new Dictionary<int, string>());

        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_SERVER_RUN")).Returns(new DomainVariableItem { Key = "TEAMSPEAK_SERVER_RUN", Item = true });
        _mockProcessUtilities.Setup(x => x.GetProcessesByName("ts3server")).Returns([]);
        _mockProcessUtilities.Setup(x => x.LaunchExternalProcess("TeamspeakServer", It.IsAny<string>(), It.IsAny<string>()))
                             .Callback((string _, string command, string _) => _launched.TrySetResult($"external: {command}"))
                             .Returns(Task.CompletedTask);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProcessPriorityClass>()))
                             .Callback((string executable, string arguments, ProcessPriorityClass priority) =>
                                           _launched.TrySetResult($"managed: {executable} {arguments} {priority}")
                             )
                             .Returns(1234);

        _subject = new TeamspeakManagerService(
            mockQuery.Object,
            new Mock<ITeamspeakService>().Object,
            new Mock<ITeamspeakGroupService>().Object,
            _mockProcessUtilities.Object,
            _mockVariablesService.Object,
            new Mock<IUksfLogger>().Object
        );
    }

    [Fact]
    public async Task Loop_starts_a_missing_teamspeak_server_at_above_normal_priority()
    {
        var serverPath = OperatingSystem.IsWindows() ? "C:/Server/Teamspeak/ts3server.exe" : "/srv/teamspeak/ts3server";
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_SERVER_PATH"))
                             .Returns(new DomainVariableItem { Key = "TEAMSPEAK_SERVER_PATH", Item = serverPath });

        _subject.Start();
        var launch = await _launched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _subject.Stop();

        launch.Should()
              .Be(
                  OperatingSystem.IsWindows()
                      ? "external: start \"\" /abovenormal \"C:/Server/Teamspeak/ts3server.exe\""
                      : "managed: /srv/teamspeak/ts3server license_accepted=1 AboveNormal"
              );
    }
}
