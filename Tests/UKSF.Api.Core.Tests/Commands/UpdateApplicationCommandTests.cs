using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Commands;

public class UpdateApplicationCommandTests
{
    private const string AccountId = "accountId";
    private const string RecruiterId = "recruiterId";
    private const string SessionUserId = "sessionUserId";
    private const string SessionUserDisplayName = "Session User";
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IAssignmentService> _mockAssignmentService;
    private readonly Mock<IHttpContextService> _mockHttpContextService;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly Mock<INotificationsService> _mockNotificationsService;
    private readonly Mock<IRecruitmentService> _mockRecruitmentService;
    private readonly UpdateApplicationCommand _subject;

    public UpdateApplicationCommandTests()
    {
        _mockAccountContext = new Mock<IAccountContext>();
        _mockAssignmentService = new Mock<IAssignmentService>();
        _mockNotificationsService = new Mock<INotificationsService>();
        _mockRecruitmentService = new Mock<IRecruitmentService>();
        _mockHttpContextService = new Mock<IHttpContextService>();
        _mockLogger = new Mock<IUksfLogger>();

        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(SessionUserId);
        _mockHttpContextService.Setup(x => x.GetUserDisplayName(false)).Returns(SessionUserDisplayName);
        _mockRecruitmentService.Setup(x => x.GetRecruiterLeadAccountIds()).Returns(new List<string>());
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole(
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>()
                                     )
                              )
                              .ReturnsAsync(new DomainNotification());

        _subject = new UpdateApplicationCommand(
            _mockAccountContext.Object,
            _mockAssignmentService.Object,
            _mockNotificationsService.Object,
            _mockRecruitmentService.Object,
            _mockHttpContextService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Accept_should_set_membership_state_to_member()
    {
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Accepted);

        _mockAccountContext.Verify(x => x.Update(AccountId, It.Is<UpdateDefinition<DomainAccount>>(u => u != null)), Times.Exactly(2));
    }

    [Fact]
    public async Task Accept_should_call_assignment_service_with_correct_values()
    {
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Accepted);

        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole(
                AccountId,
                "Basic Training Unit",
                "Trainee",
                "Recruit",
                It.IsAny<string>(),
                It.IsAny<string>(),
                "your application was accepted"
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Accept_should_add_assignment_notification()
    {
        var notification = new DomainNotification { Message = "accepted" };
        GivenAnAccountWithApplication(ApplicationState.Waiting);
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole(
                                         AccountId,
                                         "Basic Training Unit",
                                         "Trainee",
                                         "Recruit",
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         "your application was accepted"
                                     )
                              )
                              .ReturnsAsync(notification);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Accepted);

        _mockNotificationsService.Verify(x => x.Add(notification), Times.Once);
    }

    [Fact]
    public async Task Reject_should_set_membership_state_to_confirmed()
    {
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Rejected);

        _mockAccountContext.Verify(x => x.Update(AccountId, It.Is<UpdateDefinition<DomainAccount>>(u => u != null)), Times.Exactly(2));
    }

    [Fact]
    public async Task Reject_should_call_assignment_service_with_remove_flags()
    {
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Rejected);

        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole(
                AccountId,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                "",
                "Unfortunately you have not been accepted into our unit, however we thank you for your interest and hope you find a suitable alternative.",
                It.IsAny<string>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Reject_should_set_notification_link_to_application()
    {
        var notification = new DomainNotification { Message = "rejected" };
        GivenAnAccountWithApplication(ApplicationState.Waiting);
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole(
                                         AccountId,
                                         AssignmentService.RemoveFlag,
                                         AssignmentService.RemoveFlag,
                                         AssignmentService.RemoveFlag,
                                         "",
                                         It.IsAny<string>(),
                                         It.IsAny<string>()
                                     )
                              )
                              .ReturnsAsync(notification);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Rejected);

        notification.Link.Should().Be("/application");
    }

    [Fact]
    public async Task Reactivate_should_call_assignment_service_with_correct_values()
    {
        GivenAnAccountWithApplication(ApplicationState.Rejected);
        _mockRecruitmentService.Setup(x => x.GetRecruiterAccounts(false)).Returns(new List<DomainAccount> { new() { Id = RecruiterId } });

        await _subject.ExecuteAsync(AccountId, ApplicationState.Waiting);

        _mockAssignmentService.Verify(
            x => x.UpdateUnitRankAndRole(
                AccountId,
                AssignmentService.RemoveFlag,
                "Applicant",
                "Candidate",
                It.IsAny<string>(),
                It.IsAny<string>(),
                "your application was reactivated"
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Reactivate_should_reassign_recruiter_when_no_longer_active()
    {
        const string newRecruiterId = "newRecruiterId";
        GivenAnAccountWithApplication(ApplicationState.Rejected);
        _mockRecruitmentService.Setup(x => x.GetRecruiterAccounts(false)).Returns(new List<DomainAccount>());
        _mockRecruitmentService.Setup(x => x.GetNextRecruiterForApplication()).Returns(newRecruiterId);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Waiting);

        _mockAccountContext.Verify(x => x.Update(AccountId, It.Is<UpdateDefinition<DomainAccount>>(u => u != null)), Times.Exactly(3));
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("reassigning") && s.Contains(newRecruiterId))), Times.Once);
    }

    [Fact]
    public async Task Reactivate_should_keep_recruiter_when_still_active()
    {
        GivenAnAccountWithApplication(ApplicationState.Rejected);
        _mockRecruitmentService.Setup(x => x.GetRecruiterAccounts(false)).Returns(new List<DomainAccount> { new() { Id = RecruiterId } });

        await _subject.ExecuteAsync(AccountId, ApplicationState.Waiting);

        _mockAccountContext.Verify(x => x.Update(AccountId, It.Is<UpdateDefinition<DomainAccount>>(u => u != null)), Times.Exactly(2));
        _mockRecruitmentService.Verify(x => x.GetNextRecruiterForApplication(), Times.Never);
    }

    [Fact]
    public async Task Should_notify_recruiter_when_session_user_is_not_the_recruiter()
    {
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Accepted);

        _mockNotificationsService.Verify(
            x => x.Add(
                It.Is<DomainNotification>(n => n.Owner == RecruiterId &&
                                               n.Icon == NotificationIcons.Application &&
                                               n.Message.Contains("was Accepted") &&
                                               n.Message.Contains(SessionUserDisplayName) &&
                                               n.Link == $"/recruitment/{AccountId}"
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Should_not_notify_recruiter_when_session_user_is_the_recruiter()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(RecruiterId);
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Accepted);

        _mockNotificationsService.Verify(x => x.Add(It.Is<DomainNotification>(n => n.Owner == RecruiterId)), Times.Never);
    }

    [Fact]
    public async Task Should_notify_other_recruiter_leads_excluding_session_user_and_assigned_recruiter()
    {
        const string leadId1 = "leadId1";
        const string leadId2 = "leadId2";
        _mockRecruitmentService.Setup(x => x.GetRecruiterLeadAccountIds())
        .Returns(
            new List<string>
            {
                SessionUserId,
                RecruiterId,
                leadId1,
                leadId2
            }
        );
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        await _subject.ExecuteAsync(AccountId, ApplicationState.Accepted);

        _mockNotificationsService.Verify(x => x.Add(It.Is<DomainNotification>(n => n.Owner == leadId1 && n.Icon == NotificationIcons.Application)), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(It.Is<DomainNotification>(n => n.Owner == leadId2 && n.Icon == NotificationIcons.Application)), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(It.Is<DomainNotification>(n => n.Owner == SessionUserId)), Times.Never);
    }

    [Fact]
    public async Task Should_throw_bad_request_for_invalid_state()
    {
        GivenAnAccountWithApplication(ApplicationState.Waiting);

        var act = () => _subject.ExecuteAsync(AccountId, (ApplicationState)99);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*invalid*");
    }

    private void GivenAnAccountWithApplication(ApplicationState currentState)
    {
        var account = new DomainAccount
        {
            Id = AccountId,
            Firstname = "John",
            Lastname = "Doe",
            Application = new DomainApplication { State = currentState, Recruiter = RecruiterId }
        };

        _mockAccountContext.Setup(x => x.GetSingle(AccountId)).Returns(account);
    }
}
