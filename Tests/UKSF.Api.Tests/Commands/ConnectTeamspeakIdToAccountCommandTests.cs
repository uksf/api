using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Tests.Commands;

public class ConnectTeamspeakIdToAccountCommandTests
{
    private readonly string _accountId = ObjectId.GenerateNewId().ToString();
    private readonly string _confirmationCode = ObjectId.GenerateNewId().ToString();
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IConfirmationCodeService> _mockConfirmationCodeService;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly Mock<INotificationsService> _mockNotificationsService;
    private readonly ConnectTeamspeakIdToAccountCommand _subject;
    private const string TeamspeakId = "2";

    public ConnectTeamspeakIdToAccountCommandTests()
    {
        _mockEventBus = new();
        _mockLogger = new();
        _mockAccountContext = new();
        _mockConfirmationCodeService = new();
        _mockNotificationsService = new();

        _subject = new(
            _mockEventBus.Object,
            _mockLogger.Object,
            _mockAccountContext.Object,
            _mockConfirmationCodeService.Object,
            _mockNotificationsService.Object
        );
    }

    [Fact]
    public async Task When_connecting_teamspeak_id()
    {
        _mockConfirmationCodeService.Setup(x => x.GetConfirmationCodeValue(_confirmationCode)).ReturnsAsync(TeamspeakId);

        var expectedUpdate = Builders<DomainAccount>.Update.Set(x => x.TeamspeakIdentities, new() { 2 }).RenderUpdate();
        BsonValue createdUpdate = null;
        _mockAccountContext.Setup(x => x.Update(_accountId, It.IsAny<UpdateDefinition<DomainAccount>>()))
                           .Callback((string _, UpdateDefinition<DomainAccount> update) => createdUpdate = update.RenderUpdate());
        _mockAccountContext.Setup(x => x.GetSingle(_accountId))
                           .Returns(
                               () =>
                               {
                                   DomainAccount domainAccount = new() { Id = _accountId };
                                   if (createdUpdate != null)
                                   {
                                       domainAccount.TeamspeakIdentities = new() { 2 };
                                       domainAccount.Email = "test@test.com";
                                   }

                                   return domainAccount;
                               }
                           );

        var result = await _subject.ExecuteAsync(new(_accountId, TeamspeakId, _confirmationCode));

        result.TeamspeakIdentities.Single().Should().Be(2);
        createdUpdate.Should().BeEquivalentTo(expectedUpdate);

        _mockConfirmationCodeService.Verify(x => x.ClearConfirmationCodes(It.IsAny<Func<ConfirmationCode, bool>>()), Times.Never);
        _mockEventBus.Verify(x => x.Send(It.Is<DomainAccount>(m => m.TeamspeakIdentities.Single() == 2)), Times.Once);
        _mockNotificationsService.Verify(
            x => x.SendTeamspeakNotification(
                It.Is<HashSet<int>>(m => m.Single() == 2),
                "This teamspeak identity has been linked to the account with email 'test@test.com'\nIf this was not done by you, please contact an admin"
            ),
            Times.Once
        );
        _mockLogger.Verify(x => x.LogAudit($"Teamspeak ID {TeamspeakId} added for {_accountId}", null), Times.Once);
    }

    [Fact]
    public async Task When_connecting_teamspeak_id_and_code_is_null()
    {
        _mockAccountContext.Setup(x => x.GetSingle(_accountId)).Returns(new DomainAccount());
        _mockConfirmationCodeService.Setup(x => x.GetConfirmationCodeValue(_confirmationCode)).ReturnsAsync((string)null);

        Func<Task> act = async () => await _subject.ExecuteAsync(new(_accountId, TeamspeakId, _confirmationCode));

        await act.Should()
                 .ThrowAsync<InvalidConfirmationCodeException>()
                 .WithMessageAndStatusCode("Confirmation code was invalid or expired. Please try again", 400);
        _mockConfirmationCodeService.Verify(
            x => x.ClearConfirmationCodes(It.Is<Func<ConfirmationCode, bool>>(m => m(new() { Value = TeamspeakId }))),
            Times.Once
        );
    }
}
