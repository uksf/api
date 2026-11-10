using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Query;
using UKSF.Api.Integrations.Teamspeak.Services;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakManagerServiceTests
{
    private readonly List<(int ClientDbId, List<int> Groups)> _checkedGroups = [];
    private readonly Mock<ITeamspeakGroupService> _mockGroupService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<ITeamspeakQuery> _mockQuery = new();
    private readonly Mock<ITeamspeakService> _mockTeamspeakService = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly List<HashSet<TeamspeakClient>> _publishedClients = [];
    private readonly TeamspeakManagerService _subject;
    private List<TeamspeakQueryClient> _clients = [new(7, 70, "Sgt.Smith", 3, [10, 11])];

    public TeamspeakManagerServiceTests()
    {
        _mockQuery.Setup(x => x.GetClients()).ReturnsAsync(() => _clients);
        _mockQuery.Setup(x => x.GetChannelNames()).ReturnsAsync(new Dictionary<int, string> { [3] = "Briefing Room" });
        _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()))
                             .Returns(Task.CompletedTask)
                             .Callback((HashSet<TeamspeakClient> clients) => _publishedClients.Add(clients));
        _mockGroupService.Setup(x => x.CheckClientGroups(It.IsAny<int>(), It.IsAny<ICollection<int>>()))
                         .Returns(Task.CompletedTask)
                         .Callback((int clientDbId, ICollection<int> groups) => _checkedGroups.Add((clientDbId, groups.ToList())));
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_SERVER_RUN")).Returns(new DomainVariableItem { Key = "TEAMSPEAK_SERVER_RUN", Item = false });

        _subject = new TeamspeakManagerService(
            _mockQuery.Object,
            _mockTeamspeakService.Object,
            _mockGroupService.Object,
            new Mock<IProcessUtilities>().Object,
            _mockVariablesService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Sync_publishes_online_clients_with_channel_names()
    {
        await _subject.Sync();

        _publishedClients.Should().ContainSingle();
        _publishedClients[0]
        .Should()
        .BeEquivalentTo(
            [
                new TeamspeakClient
                {
                    ClientDbId = 70,
                    ClientName = "Sgt.Smith",
                    ChannelId = 3,
                    ChannelName = "Briefing Room"
                }
            ]
        );
    }

    [Fact]
    public async Task Sync_does_not_republish_unchanged_clients()
    {
        await _subject.Sync();
        await _subject.Sync();

        _publishedClients.Should().HaveCount(1);
    }

    [Fact]
    public async Task Sync_republishes_when_a_client_moves_channel()
    {
        await _subject.Sync();
        _clients = [new(7, 70, "Sgt.Smith", 4, [10, 11])];
        await _subject.Sync();

        _publishedClients.Should().HaveCount(2);
        _publishedClients[1].Single().ChannelId.Should().Be(4);
    }

    [Fact]
    public async Task Sync_checks_groups_once_for_a_client_until_its_groups_change()
    {
        await _subject.Sync();
        await _subject.Sync();
        _clients = [new(7, 70, "Sgt.Smith", 3, [10])];
        await _subject.Sync();

        _checkedGroups.Should().BeEquivalentTo([(70, new List<int> { 10, 11 }), (70, new List<int> { 10 })]);
    }

    [Fact]
    public async Task Sync_checks_groups_again_when_a_client_reconnects()
    {
        await _subject.Sync();
        _clients = [new(8, 70, "Sgt.Smith", 3, [10, 11])];
        await _subject.Sync();

        _checkedGroups.Should().HaveCount(2);
    }

    [Fact]
    public async Task Sync_retries_a_failed_group_check_and_logs_it()
    {
        _mockGroupService.Setup(x => x.CheckClientGroups(70, It.IsAny<ICollection<int>>())).ThrowsAsync(new InvalidOperationException("boom"));

        await _subject.Sync();
        await _subject.Sync();

        _mockGroupService.Verify(x => x.CheckClientGroups(70, It.IsAny<ICollection<int>>()), Times.Exactly(2));
        _mockLogger.Verify(x => x.LogError("Teamspeak server group check failed for client 70", It.IsAny<Exception>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Reload_republishes_clients_and_rechecks_groups()
    {
        await _subject.Sync();
        _subject.Reload();
        await _subject.Sync();

        _publishedClients.Should().HaveCount(2);
        _checkedGroups.Should().HaveCount(2);
    }

    [Fact]
    public void Start_does_not_start_the_loop_when_teamspeak_is_disabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(false);

        _subject.Start();
        _subject.Stop();

        _mockQuery.Verify(x => x.Disconnect(), Times.Never);
    }

    [Fact]
    public void Start_then_stop_disconnects_the_query()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);

        _subject.Start();
        _subject.Stop();

        _mockQuery.Verify(x => x.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task Loop_logs_a_failed_sync_once_and_keeps_running()
    {
        var secondAttempt = new TaskCompletionSource();
        var attempts = 0;
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);
        _mockTeamspeakService.Setup(x => x.Enabled).Returns(true);
        _mockQuery.Setup(x => x.GetClients())
                  .Callback(() =>
                      {
                          if (++attempts == 2)
                          {
                              secondAttempt.TrySetResult();
                          }
                      }
                  )
                  .ThrowsAsync(new System.IO.IOException("closed"));

        _subject.Start();
        await secondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(15));
        _subject.Stop();

        _mockLogger.Verify(x => x.LogError("Teamspeak sync failed", It.IsAny<Exception>()), Times.Once);
    }
}
