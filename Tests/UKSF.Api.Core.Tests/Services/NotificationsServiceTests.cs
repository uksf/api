using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Queries;
using UKSF.Api.Core.Services;
using UKSF.Api.Core.Signalr.Clients;
using UKSF.Api.Core.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class NotificationsServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<INotificationsContext> _mockNotificationsContext = new();
    private readonly Mock<ISendTemplatedEmailCommand> _mockSendEmailCommand = new();
    private readonly Mock<IHubContext<NotificationHub, INotificationsClient>> _mockHub = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly Mock<IObjectIdConversionService> _mockObjectIdConversionService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<INotificationsClient> _mockClient = new();
    private readonly NotificationsService _subject;

    public NotificationsServiceTests()
    {
        Mock<IHubClients<INotificationsClient>> mockHubClients = new();
        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClient.Object);

        _mockVariablesService.Setup(x => x.GetFeatureState("NOTIFICATIONS")).Returns(true);
        _mockObjectIdConversionService.Setup(x => x.ConvertObjectIds(It.IsAny<string>())).Returns<string>(x => x);

        _subject = new NotificationsService(
            _mockAccountContext.Object,
            _mockNotificationsContext.Object,
            _mockSendEmailCommand.Object,
            _mockHub.Object,
            _mockHttpContextService.Object,
            _mockObjectIdConversionService.Object,
            _mockEventBus.Object,
            _mockVariablesService.Object
        );
    }

    [Fact]
    public void Add_ShouldDoNothing_WhenNotificationIsNull()
    {
        _subject.Add(null);

        _mockNotificationsContext.Verify(x => x.Add(It.IsAny<DomainNotification>()), Times.Never);
    }

    [Fact]
    public void Add_ShouldNotThrow_WhenValid()
    {
        var account = new DomainAccount
        {
            Id = "user1",
            MembershipState = MembershipState.Member,
            Settings = new AccountSettings { NotificationsEmail = false, NotificationsTeamspeak = false }
        };
        _mockAccountContext.Setup(x => x.GetSingle("user1")).Returns(account);

        var notification = new DomainNotification { Owner = "user1", Message = "Test" };

        var act = () => _subject.Add(notification);

        act.Should().NotThrow();
    }

    [Fact]
    public void GetNotificationsForContext_ShouldReturnUserNotifications()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        var notifications = new List<DomainNotification>
        {
            new()
            {
                Id = "n1",
                Owner = "user1",
                Message = "Test 1"
            },
            new()
            {
                Id = "n2",
                Owner = "user1",
                Message = "Test 2"
            }
        };
        _mockNotificationsContext.Setup(x => x.Get(It.IsAny<Func<DomainNotification, bool>>())).Returns(notifications);

        var result = _subject.GetNotificationsForContext();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task MarkNotificationsAsRead_ShouldUpdateAndNotifyViaSignalR()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        var ids = new List<string> { "n1", "n2" };

        await _subject.MarkNotificationsAsRead(ids);

        _mockNotificationsContext.Verify(
            x => x.UpdateMany(It.IsAny<Expression<Func<DomainNotification, bool>>>(), It.IsAny<UpdateDefinition<DomainNotification>>()),
            Times.Once
        );
        _mockClient.Verify(x => x.ReceiveRead(ids), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldDeleteAndNotifyViaSignalR()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        var ids = new List<string> { "n1", "n2" };

        await _subject.Delete(ids);

        _mockNotificationsContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNotification, bool>>>()), Times.Once);
        _mockClient.Verify(x => x.ReceiveClear(It.Is<List<string>>(l => l.Count == 2)), Times.Once);
    }

    [Fact]
    public void SendTeamspeakNotification_ShouldDoNothing_WhenNotificationsDisabled()
    {
        _mockVariablesService.Setup(x => x.GetFeatureState("NOTIFICATIONS")).Returns(false);

        _subject.SendTeamspeakNotification(new List<int> { 1, 2 }, "test message");

        _mockEventBus.Verify(x => x.Send(It.IsAny<TeamspeakMessageEventData>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SendTeamspeakNotification_ShouldSendEvent_WhenNotificationsEnabled()
    {
        _subject.SendTeamspeakNotification(new List<int> { 1, 2 }, "test message");

        _mockEventBus.Verify(x => x.Send(It.Is<TeamspeakMessageEventData>(d => d.Message == "test message"), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void SendTeamspeakNotification_ShouldReplaceHtmlLinksWithTeamspeakFormat()
    {
        _subject.SendTeamspeakNotification(new List<int> { 1 }, "<a href='http://example.com'>click</a>");

        _mockEventBus.Verify(
            x => x.Send(It.Is<TeamspeakMessageEventData>(d => d.Message == "[url]http://example.com[/url]click</a>"), It.IsAny<string>()),
            Times.Once
        );
    }
}
