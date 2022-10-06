using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Services;
using UKSF.Api.Teamspeak.Signalr.Clients;
using UKSF.Api.Teamspeak.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly Mock<IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient>> _mockHubContext;
    private readonly Mock<IMongoDatabase> _mockMongoDatabase;
    private readonly Mock<ITeamspeakManagerService> _mockTeamspeakManagerService;
    private readonly TeamspeakService _subject;

    public TeamspeakServiceTests()
    {
        _mockAccountContext = new();
        _mockMongoDatabase = new();
        _mockHubContext = new();
        _mockTeamspeakManagerService = new();
        _mockHostEnvironment = new();

        _subject = new(
            _mockAccountContext.Object,
            _mockMongoDatabase.Object,
            _mockHubContext.Object,
            _mockTeamspeakManagerService.Object,
            _mockHostEnvironment.Object
        );
    }

    [Fact]
    public void When_getting_formatted_clients()
    {
        var accounts = new List<DomainAccount> { new() { TeamspeakIdentities = new() { 2 } } };

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
