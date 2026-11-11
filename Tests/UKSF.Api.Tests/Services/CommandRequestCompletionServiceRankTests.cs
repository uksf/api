using System.Threading.Tasks;
using Moq;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Tests.Services;

public partial class CommandRequestCompletionServiceTests
{
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

    [Fact]
    public async Task Resolve_WhenPromotion_RecruitToTrooper_ShouldReturnRifleman()
    {
        var id = "request-1";
        var request = CreateRequest(id, CommandRequestType.Promotion, recipient: "account-1", value: "Trooper");
        SetupApproved(id, request);
        SetupAccountForRank("account-1", "Recruit", "Trainee");
        var notification = new DomainNotification();
        _mockAssignmentService.Setup(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Trooper", "", "", "")).ReturnsAsync(notification);

        await _subject.Resolve(id);

        _mockAssignmentService.Verify(x => x.UpdateUnitRankAndRole("account-1", "", "Rifleman", "Trooper", "", "", ""), Times.Once);
    }
}
