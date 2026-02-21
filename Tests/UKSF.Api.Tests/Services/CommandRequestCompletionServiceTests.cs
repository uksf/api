using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class CommandRequestCompletionServiceTests
{
    private readonly Mock<IDischargeContext> _mockDischargeContext = new();
    private readonly Mock<ICommandRequestContext> _mockCommandRequestContext = new();
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly Mock<ICommandRequestService> _mockCommandRequestService = new();
    private readonly Mock<IAssignmentService> _mockAssignmentService = new();
    private readonly Mock<ILoaService> _mockLoaService = new();
    private readonly Mock<IUnitsService> _mockUnitsService = new();
    private readonly Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>> _mockHub = new();
    private readonly Mock<ICommandRequestsClient> _mockHubClients = new();
    private readonly Mock<INotificationsService> _mockNotificationsService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly CommandRequestCompletionService _subject;

    public CommandRequestCompletionServiceTests()
    {
        _mockHubClients.Setup(x => x.ReceiveRequestUpdate()).Returns(Task.CompletedTask);
        _mockHub.Setup(x => x.Clients.All).Returns(_mockHubClients.Object);

        _subject = new CommandRequestCompletionService(
            _mockDischargeContext.Object,
            _mockCommandRequestContext.Object,
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockHttpContextService.Object,
            _mockCommandRequestService.Object,
            _mockAssignmentService.Object,
            _mockLoaService.Object,
            _mockUnitsService.Object,
            _mockHub.Object,
            _mockNotificationsService.Object,
            _mockLogger.Object
        );
    }

    #region Resolve entry point

    [Fact]
    public async Task Resolve_ShouldOnlySendSignalRUpdate_WhenNeitherApprovedNorRejected()
    {
        var id = "request-1";
        _mockCommandRequestService.Setup(x => x.IsRequestApproved(id)).Returns(false);
        _mockCommandRequestService.Setup(x => x.IsRequestRejected(id)).Returns(false);

        await _subject.Resolve(id);

        _mockHubClients.Verify(x => x.ReceiveRequestUpdate(), Times.Once);
        _mockCommandRequestContext.Verify(x => x.GetSingle(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_ShouldThrowBadRequestException_WhenTypeUnrecognized()
    {
        var id = "request-1";
        var request = CreateRequest(id, "UnknownType");
        SetupApproved(id, request);

        var act = () => _subject.Resolve(id);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Request type not recognized: 'UnknownType'");
        _mockHubClients.Verify(x => x.ReceiveRequestUpdate(), Times.Never);
    }

    #endregion

    #region Rank (Promotion/Demotion)

    [Fact]
    public async Task Resolve_WhenPromotion_AndApproved_ShouldUpdateRankAndNotifyAndArchiveAndLog()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Promotion, recipient: "account-1", value: "Corporal", reason: "Good work");
        SetupApproved(id, request);
        SetupAccountForRank("account-1", "Private", "Rifleman");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Corporal", "", "", "Good work")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Corporal", "", "", "Good work"), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(notification), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("approved"))), Times.Once);
        _mockHubClients.Verify(x => x.ReceiveRequestUpdate(), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenPromotion_AndApproved_RecruitToPrivate_ShouldPassRiflemanAsRole()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Promotion, recipient: "account-1", value: "Private");
        SetupApproved(id, request);
        SetupAccountForRank("account-1", "Recruit", "");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Private", "", "", "")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Private", "", "", ""), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenPromotion_AndApproved_NonRecruit_ShouldPassExistingRoleAssignment()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Promotion, recipient: "account-1", value: "Sergeant");
        SetupApproved(id, request);
        SetupAccountForRank("account-1", "Corporal", "Team Leader");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Team Leader", "Sergeant", "", "", "")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Team Leader", "Sergeant", "", "", ""), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenDemotion_AndApproved_ShouldUpdateRankAndArchive()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Demotion, recipient: "account-1", value: "Private");
        SetupApproved(id, request);
        SetupAccountForRank("account-1", "Corporal", "Team Leader");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Team Leader", "Private", "", "", "")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Team Leader", "Private", "", "", ""), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenPromotion_AndRejected_ShouldArchiveAndLogWithoutUpdatingRank()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Promotion);
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Never
        );
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("rejected"))), Times.Once);
        _mockHubClients.Verify(x => x.ReceiveRequestUpdate(), Times.Once);
    }

    #endregion

    #region Loa

    [Fact]
    public async Task Resolve_WhenLoa_AndApproved_ShouldSetStateApprovedAndArchiveAndLogWithFormattedDates()
    {
        var id = "request-1";
        var startDate = new DateTime(2025, 3, 15);
        var endDate = new DateTime(2025, 4, 20);
        var request = CreateRequest(id, CommandRequestType.Loa, value: "loa-1", displayFrom: startDate.ToString("O"), displayValue: endDate.ToString("O"));
        SetupApproved(id, request);

        await _subject.Resolve(id);

        _mockLoaService.Verify(x => x.SetLoaState("loa-1", LoaReviewState.Approved), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("approved") && s.Contains("15 Mar 2025") && s.Contains("20 Apr 2025"))), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenLoa_AndRejected_ShouldSetStateRejectedAndArchiveAndLog()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Loa, value: "loa-1", displayFrom: "2025-03-15", displayValue: "2025-04-20");
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockLoaService.Verify(x => x.SetLoaState("loa-1", LoaReviewState.Rejected), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("rejected"))), Times.Once);
    }

    #endregion

    #region Discharge

    [Fact]
    public async Task Resolve_WhenDischarge_AndApproved_NoExistingDischargeCollection_ShouldCreateNewCollectionAndDischarge()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Discharge, recipient: "account-1", reason: "Inactive", displayRequester: "Admin User");
        SetupApproved(id, request);
        var account = new DomainAccount
        {
            Id = "account-1",
            Rank = "Private",
            UnitAssignment = "1 Section",
            RoleAssignment = "Rifleman",
            Firstname = "John",
            Lastname = "Smith"
        };
        _mockAccountContext.Setup(x => x.GetSingle("account-1")).Returns(account);
        _mockDischargeContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainDischargeCollection, bool>>())).Returns((DomainDischargeCollection)null);
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole(
                                         "account-1",
                                         AssignmentService.RemoveFlag,
                                         AssignmentService.RemoveFlag,
                                         AssignmentService.RemoveFlag,
                                         "Inactive",
                                         "",
                                         AssignmentService.RemoveFlag
                                     )
                              )
                              .ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockDischargeContext.Verify(
            x => x.Add(
                It.Is<DomainDischargeCollection>(dc => dc.AccountId == "account-1" &&
                                                       dc.Name == "Smith.J" &&
                                                       dc.Discharges.Count == 1 &&
                                                       dc.Discharges[0].Rank == "Private" &&
                                                       dc.Discharges[0].Unit == "1 Section" &&
                                                       dc.Discharges[0].Role == "Rifleman" &&
                                                       dc.Discharges[0].DischargedBy == "Admin User" &&
                                                       dc.Discharges[0].Reason == "Inactive"
                )
            ),
            Times.Once
        );
        _mockAccountContext.Verify(
            x => x.Update("account-1", It.IsAny<Expression<Func<DomainAccount, MembershipState>>>(), MembershipState.Discharged),
            Times.Once
        );
        _mockAssignmentService.Verify(x => x.UnassignAllUnits("account-1"), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(notification), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenDischarge_AndApproved_ExistingDischargeCollection_ShouldUpdateExistingAndSetReinstatedFalse()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Discharge, recipient: "account-1", reason: "Inactive", displayRequester: "Admin User");
        SetupApproved(id, request);
        var account = new DomainAccount
        {
            Id = "account-1",
            Rank = "Corporal",
            UnitAssignment = "2 Section",
            RoleAssignment = "Team Leader",
            Firstname = "Jane",
            Lastname = "Doe"
        };
        _mockAccountContext.Setup(x => x.GetSingle("account-1")).Returns(account);
        var existingCollection = new DomainDischargeCollection
        {
            Id = "dc-1",
            AccountId = "account-1",
            Name = "Doe.J",
            Reinstated = true,
            Discharges = new List<DomainDischarge> { new() { Rank = "Private", Reason = "Previous discharge" } }
        };
        _mockDischargeContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainDischargeCollection, bool>>())).Returns(existingCollection);
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole(
                                         "account-1",
                                         AssignmentService.RemoveFlag,
                                         AssignmentService.RemoveFlag,
                                         AssignmentService.RemoveFlag,
                                         "Inactive",
                                         "",
                                         AssignmentService.RemoveFlag
                                     )
                              )
                              .ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockDischargeContext.Verify(x => x.Update("dc-1", It.IsAny<UpdateDefinition<DomainDischargeCollection>>()), Times.Once);
        _mockDischargeContext.Verify(x => x.Add(It.IsAny<DomainDischargeCollection>()), Times.Never);
        existingCollection.Discharges.Should().HaveCount(2);
        existingCollection.Discharges[1].Rank.Should().Be("Corporal");
    }

    [Fact]
    public async Task Resolve_WhenDischarge_AndRejected_ShouldOnlyArchive()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Discharge);
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockDischargeContext.Verify(x => x.Add(It.IsAny<DomainDischargeCollection>()), Times.Never);
        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Never
        );
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("rejected"))), Times.Once);
    }

    #endregion

    #region IndividualRole

    [Fact]
    public async Task Resolve_WhenRole_AndApproved_WithNone_ShouldPassRemoveFlag()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Role, recipient: "account-1", value: "None", reason: "No longer needed");
        SetupApproved(id, request);
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", AssignmentService.RemoveFlag, "", "", "", "No longer needed"))
                              .ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", AssignmentService.RemoveFlag, "", "", "", "No longer needed"), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(notification), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenRole_AndApproved_WithActualRole_ShouldPassRoleValue()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Role, recipient: "account-1", value: "Medic", reason: "Qualified");
        SetupApproved(id, request);
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Medic", "", "", "", "Qualified")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Medic", "", "", "", "Qualified"), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(notification), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenRole_AndRejected_ShouldOnlyArchive()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Role);
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Never
        );
    }

    #endregion

    #region ChainOfCommandPosition

    [Fact]
    public async Task Resolve_WhenChainOfCommandPosition_AndApproved_SecondaryValueNone_EmptyValue_ShouldUnassignAll()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.ChainOfCommandPosition, recipient: "account-1", value: "", secondaryValue: "None");
        SetupApproved(id, request);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UnassignAllUnitChainOfCommandPositions("account-1"), Times.Once);
        _mockNotificationsService.Verify(
            x => x.Add(
                It.Is<DomainNotification>(n => n.Owner == "account-1" &&
                                               n.Message == "You have been unassigned from all chain of command positions in all units" &&
                                               n.Icon == NotificationIcons.Demotion
                )
            ),
            Times.Once
        );
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenChainOfCommandPosition_AndApproved_SecondaryValueNone_WithValue_ShouldUnassignFromUnit()
    {
        var id = "request-1";
        var unitId = "unit-1";
        var request = CreateRequest(id, CommandRequestType.ChainOfCommandPosition, recipient: "account-1", value: unitId, secondaryValue: "None");
        SetupApproved(id, request);
        var unit = new DomainUnit { Id = unitId, Name = "1 Platoon" };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        _mockAssignmentService.Setup(x => x.UnassignUnitChainOfCommandPosition("account-1", unitId)).ReturnsAsync("Commander");
        _mockUnitsService.Setup(x => x.GetChainString(unit)).Returns("1 Platoon");

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UnassignUnitChainOfCommandPosition("account-1", unitId), Times.Once);
        _mockNotificationsService.Verify(
            x => x.Add(
                It.Is<DomainNotification>(n => n.Owner == "account-1" &&
                                               n.Message.Contains("unassigned as") &&
                                               n.Message.Contains("Commander") &&
                                               n.Message.Contains("1 Platoon") &&
                                               n.Icon == NotificationIcons.Demotion
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Resolve_WhenChainOfCommandPosition_AndApproved_WithSecondaryValue_ShouldAssignPosition()
    {
        var id = "request-1";
        var unitId = "unit-1";
        var request = CreateRequest(id, CommandRequestType.ChainOfCommandPosition, recipient: "account-1", value: unitId, secondaryValue: "Commander");
        SetupApproved(id, request);
        var unit = new DomainUnit { Id = unitId, Name = "1 Platoon" };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        _mockUnitsService.Setup(x => x.GetChainString(unit)).Returns("1 Platoon");

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.AssignUnitChainOfCommandPosition("account-1", unitId, "Commander"), Times.Once);
        _mockNotificationsService.Verify(
            x => x.Add(
                It.Is<DomainNotification>(n => n.Owner == "account-1" &&
                                               n.Message.Contains("assigned as") &&
                                               n.Message.Contains("Commander") &&
                                               n.Message.Contains("1 Platoon") &&
                                               n.Icon == NotificationIcons.Promotion
                )
            ),
            Times.Once
        );
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenChainOfCommandPosition_AndRejected_ShouldOnlyArchive()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.ChainOfCommandPosition, value: "unit-1");
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockAssignmentService.Verify(x => x.AssignUnitChainOfCommandPosition(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockAssignmentService.Verify(x => x.UnassignUnitChainOfCommandPosition(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockAssignmentService.Verify(x => x.UnassignAllUnitChainOfCommandPositions(It.IsAny<string>()), Times.Never);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("rejected"))), Times.Once);
    }

    #endregion

    #region Transfer

    [Fact]
    public async Task Resolve_WhenTransfer_AndApproved_ShouldLookUpUnitAndUpdateRankAndRole()
    {
        var id = "request-1";
        var unitId = "unit-1";
        var request = CreateRequest(id, CommandRequestType.Transfer, recipient: "account-1", value: unitId, reason: "Reassignment");
        SetupApproved(id, request);
        var unit = new DomainUnit { Id = unitId, Name = "2 Platoon" };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "2 Platoon", "", "", "", "", "Reassignment")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "2 Platoon", "", "", "", "", "Reassignment"), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(notification), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("approved"))), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenAuxiliaryTransfer_AndApproved_ShouldLookUpUnitAndUpdate()
    {
        var id = "request-1";
        var unitId = "unit-1";
        var request = CreateRequest(id, CommandRequestType.AuxiliaryTransfer, recipient: "account-1", value: unitId, reason: "Aux transfer");
        SetupApproved(id, request);
        var unit = new DomainUnit { Id = unitId, Name = "Support" };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "Support", "", "", "", "", "Aux transfer")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "Support", "", "", "", "", "Aux transfer"), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenTransfer_AndRejected_ShouldOnlyArchive()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Transfer);
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Never
        );
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("rejected"))), Times.Once);
    }

    #endregion

    #region UnitRemoval

    [Fact]
    public async Task Resolve_WhenUnitRemoval_AndApproved_ShouldUnassignUnitAndNotify()
    {
        var id = "request-1";
        var unitId = "unit-1";
        var request = CreateRequest(id, CommandRequestType.UnitRemoval, recipient: "account-1", value: unitId, reason: "Restructuring");
        SetupApproved(id, request);
        var unit = new DomainUnit { Id = unitId, Name = "3 Section" };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        _mockUnitsService.Setup(x => x.GetChainString(unit)).Returns("3 Section");

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UnassignUnit("account-1", unitId), Times.Once);
        _mockNotificationsService.Verify(
            x => x.Add(
                It.Is<DomainNotification>(n => n.Owner == "account-1" &&
                                               n.Message == "You have been removed from 3 Section" &&
                                               n.Icon == NotificationIcons.Demotion
                )
            ),
            Times.Once
        );
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("approved"))), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenUnitRemoval_AndRejected_ShouldOnlyArchive()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.UnitRemoval);
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockAssignmentService.Verify(x => x.UnassignUnit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("rejected"))), Times.Once);
    }

    #endregion

    #region Reinstate

    [Fact]
    public async Task Resolve_WhenReinstate_AndApproved_ShouldSetReinstatedAndMemberAndAssignToBtu()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.ReinstateMember, recipient: "account-1", reason: "Returned");
        SetupApproved(id, request);
        var dischargeCollection = new DomainDischargeCollection
        {
            Id = "dc-1",
            AccountId = "account-1",
            Name = "Smith.J"
        };
        _mockDischargeContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainDischargeCollection, bool>>())).Returns(dischargeCollection);
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("admin-1");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole(
                                         "account-1",
                                         "Basic Training Unit",
                                         "Trainee",
                                         "Recruit",
                                         "",
                                         "",
                                         "your membership was reinstated"
                                     )
                              )
                              .ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockDischargeContext.Verify(x => x.Update("dc-1", It.IsAny<Expression<Func<DomainDischargeCollection, bool>>>(), true), Times.Once);
        _mockAccountContext.Verify(
            x => x.Update("account-1", It.IsAny<Expression<Func<DomainAccount, MembershipState>>>(), MembershipState.Member),
            Times.Once
        );
        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole("account-1", "Basic Training Unit", "Trainee", "Recruit", "", "", "your membership was reinstated"),
            Times.Once
        );
        _mockNotificationsService.Verify(x => x.Add(notification), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("reinstated") && s.Contains("Smith.J"))), Times.Once);
        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenReinstate_AndRejected_ShouldOnlyArchive()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.ReinstateMember);
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockCommandRequestService.Verify(x => x.ArchiveRequest(id), Times.Once);
        _mockDischargeContext.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDischargeCollection, bool>>>(), It.IsAny<bool>()),
            Times.Never
        );
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("rejected"))), Times.Once);
    }

    #endregion

    #region HandleRecruitToPrivate (tested indirectly through Rank)

    [Fact]
    public async Task Resolve_WhenPromotion_RecruitToPrivate_ShouldReturnRifleman()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Promotion, recipient: "account-1", value: "Private");
        SetupApproved(id, request);
        SetupAccountForRank("account-1", "Recruit", "SomeRole");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Private", "", "", "")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Private", "", "", ""), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenPromotion_NonRecruitToSergeant_ShouldReturnExistingRole()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Promotion, recipient: "account-1", value: "Sergeant");
        SetupApproved(id, request);
        SetupAccountForRank("account-1", "Corporal", "Medic");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Medic", "Sergeant", "", "", "")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Medic", "Sergeant", "", "", ""), Times.Once);
    }

    #endregion

    #region FormatDate (tested indirectly through Loa)

    [Fact]
    public async Task Resolve_WhenLoa_AndApproved_ValidDates_ShouldFormatAsDdMmmYyyy()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Loa, value: "loa-1", displayFrom: "2025-12-25", displayValue: "2026-01-05");
        SetupApproved(id, request);

        await _subject.Resolve(id);

        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("25 Dec 2025") && s.Contains("05 Jan 2026"))), Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenLoa_AndApproved_InvalidDateStrings_ShouldReturnOriginalStrings()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Loa, value: "loa-1", displayFrom: "not-a-date", displayValue: "also-not-a-date");
        SetupApproved(id, request);

        await _subject.Resolve(id);

        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("not-a-date") && s.Contains("also-not-a-date"))), Times.Once);
    }

    #endregion

    #region SignalR always called

    [Fact]
    public async Task Resolve_ShouldAlwaysCallReceiveRequestUpdate_WhenApproved()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Loa, value: "loa-1");
        SetupApproved(id, request);

        await _subject.Resolve(id);

        _mockHubClients.Verify(x => x.ReceiveRequestUpdate(), Times.Once);
    }

    [Fact]
    public async Task Resolve_ShouldAlwaysCallReceiveRequestUpdate_WhenRejected()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Loa, value: "loa-1");
        SetupRejected(id, request);

        await _subject.Resolve(id);

        _mockHubClients.Verify(x => x.ReceiveRequestUpdate(), Times.Once);
    }

    #endregion

    #region Helpers

    private static DomainCommandRequest CreateRequest(
        string id,
        string type,
        string recipient = "recipient-1",
        string value = "",
        string secondaryValue = "",
        string reason = "",
        string displayRecipient = "Display Recipient",
        string displayFrom = "Display From",
        string displayValue = "Display Value",
        string displayRequester = "Display Requester"
    )
    {
        return new DomainCommandRequest
        {
            Id = id,
            Type = type,
            Recipient = recipient,
            Value = value,
            SecondaryValue = secondaryValue,
            Reason = reason,
            DisplayRecipient = displayRecipient,
            DisplayFrom = displayFrom,
            DisplayValue = displayValue,
            DisplayRequester = displayRequester
        };
    }

    private void SetupApproved(string id, DomainCommandRequest request)
    {
        _mockCommandRequestService.Setup(x => x.IsRequestApproved(id)).Returns(true);
        _mockCommandRequestService.Setup(x => x.IsRequestRejected(id)).Returns(false);
        _mockCommandRequestContext.Setup(x => x.GetSingle(id)).Returns(request);
    }

    private void SetupRejected(string id, DomainCommandRequest request)
    {
        _mockCommandRequestService.Setup(x => x.IsRequestApproved(id)).Returns(false);
        _mockCommandRequestService.Setup(x => x.IsRequestRejected(id)).Returns(true);
        _mockCommandRequestContext.Setup(x => x.GetSingle(id)).Returns(request);
    }

    private void SetupAccountForRank(string accountId, string currentRank, string currentRole)
    {
        _mockAccountContext.Setup(x => x.GetSingle(accountId))
        .Returns(
            new DomainAccount
            {
                Id = accountId,
                Rank = currentRank,
                RoleAssignment = currentRole
            }
        );
    }

    #endregion
}
