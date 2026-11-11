using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Query;
using UKSF.Api.Integrations.Teamspeak.Services;
using UKSF.Api.Integrations.Teamspeak.Signalr.Clients;
using UKSF.Api.Integrations.Teamspeak.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly Mock<ITeamspeakGroupService> _mockGroupService = new();
    private readonly Mock<ITeamspeakQuery> _mockQuery = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly TeamspeakService _subject;

    public TeamspeakServiceTests()
    {
        Mock<IMongoDatabase> mockMongoDatabase = new();
        Mock<IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient>> mockHubContext = new();
        _mockVariablesService.Setup(x => x.GetFeatureState("TEAMSPEAK")).Returns(true);
        _mockQuery.Setup(x => x.Configured).Returns(true);
        _mockAccountContext = new Mock<IAccountContext>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();

        _subject = new TeamspeakService(
            _mockAccountContext.Object,
            mockMongoDatabase.Object,
            mockHubContext.Object,
            _mockQuery.Object,
            _mockGroupService.Object,
            _mockVariablesService.Object,
            _mockHostEnvironment.Object
        );
    }

    [Fact]
    public async Task Sends_a_formatted_message_only_to_online_target_clients()
    {
        _mockQuery.Setup(x => x.GetClients()).ReturnsAsync([new TeamspeakQueryClient(7, 70, "A", 1, []), new TeamspeakQueryClient(8, 80, "B", 1, [])]);

        await _subject.SendTeamspeakMessageToClient([70, 90], "hello");

        _mockQuery.Verify(x => x.SendMessage(7, "\n========== UKSF Server Message ==========\nhello\n=================================="), Times.Once);
        _mockQuery.Verify(x => x.SendMessage(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Does_not_query_when_the_query_login_is_not_configured()
    {
        _mockQuery.Setup(x => x.Configured).Returns(false);

        await _subject.SendTeamspeakMessageToClient([70], "hello");
        await _subject.UpdateAccountTeamspeakGroups(new DomainAccount { TeamspeakIdentities = [70] });

        _mockQuery.Verify(x => x.GetClients(), Times.Never);
        _mockQuery.Verify(x => x.GetServerGroups(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Updating_account_groups_checks_each_identity_with_its_current_groups()
    {
        _mockQuery.Setup(x => x.GetServerGroups(70)).ReturnsAsync([6, 8]);
        _mockQuery.Setup(x => x.GetServerGroups(71)).ReturnsAsync([]);

        await _subject.UpdateAccountTeamspeakGroups(new DomainAccount { TeamspeakIdentities = [70, 71] });

        _mockGroupService.Verify(x => x.CheckClientGroups(70, new List<int> { 6, 8 }), Times.Once);
        _mockGroupService.Verify(x => x.CheckClientGroups(71, new List<int>()), Times.Once);
    }

    [Fact]
    public void When_getting_formatted_clients()
    {
        var accounts = new List<DomainAccount> { new() { TeamspeakIdentities = [2] } };

        _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Development);
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns<Func<DomainAccount, bool>>(predicate => accounts.FirstOrDefault(predicate));

        var result = _subject.GetFormattedClients();

        result.Should()
              .SatisfyRespectively(
                  first =>
                  {
                      first.ClientName.Should().Be("Dummy Client");
                      first.Connected.Should().BeFalse();
                  },
                  second =>
                  {
                      second.ClientName.Should().Be("SqnLdr.Beswick.T");
                      second.Connected.Should().BeTrue();
                  }
              );
    }

    [Fact]
    public void When_getting_formatted_clients_with_nulls()
    {
        _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Development);
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns((DomainAccount)null);

        var result = _subject.GetFormattedClients();

        result.Should()
              .SatisfyRespectively(
                  first =>
                  {
                      first.ClientName.Should().Be("SqnLdr.Beswick.T");
                      first.Connected.Should().BeFalse();
                  },
                  second =>
                  {
                      second.ClientName.Should().Be("Dummy Client");
                      second.Connected.Should().BeFalse();
                  }
              );
    }
}
