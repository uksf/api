using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class DocumentPermissionsServiceTests
{
    private readonly string _memberId = ObjectId.GenerateNewId().ToString();
    private readonly Mock<IAccountService> _mockIAccountService = new();
    private readonly Mock<IHttpContextService> _mockIHttpContextService = new();
    private readonly Mock<IRanksService> _mockIRanksService = new();
    private readonly Mock<IUnitsService> _mockIUnitsService = new();
    private readonly DocumentPermissionsService _subject;
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();

    public DocumentPermissionsServiceTests()
    {
        _mockIHttpContextService.Setup(x => x.GetUserId()).Returns(_memberId);
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(false);
        _mockIAccountService.Setup(x => x.GetUserAccount()).Returns(new DomainAccount { Rank = "rank" });

        _subject = new(_mockIHttpContextService.Object, _mockIUnitsService.Object, _mockIRanksService.Object, _mockIAccountService.Object);
    }

    [Fact]
    public void When_checking_permission_as_superadmin()
    {
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(true);

        var result = _subject.DoesContextHaveReadPermission(new());

        result.Should().Be(true);
    }

    [Fact]
    public void When_checking_permission_for_empty()
    {
        var result = _subject.DoesContextHaveReadPermission(new());

        result.Should().Be(true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_checking_read_permission_for_units_only(bool hasPermission)
    {
        Given_unit_read_permissions(hasPermission);

        var result = _subject.DoesContextHaveReadPermission(new() { ReadPermissions = { Units = new() { _unitId } } });

        result.Should().Be(hasPermission);
    }

    [Fact]
    public void When_checking_permission_for_units_with_selected_units_only()
    {
        Given_unit_read_permissions_for_selected_units_only(true);

        var result = _subject.DoesContextHaveReadPermission(new() { ReadPermissions = { Units = new() { _unitId }, SelectedUnitsOnly = true } });

        result.Should().Be(true);
        _mockIUnitsService.Verify(x => x.AnyParentHasMember(_unitId, _memberId), Times.Never);
        _mockIUnitsService.Verify(x => x.AnyChildHasMember(_unitId, _memberId), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_checking_write_permission_for_units_only(bool hasPermission)
    {
        Given_unit_write_permissions(hasPermission);

        var result = _subject.DoesContextHaveWritePermission(new() { WritePermissions = new() { Units = new() { _unitId } } });

        result.Should().Be(hasPermission);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_checking_permission_for_rank_only(bool hasPermission)
    {
        Given_rank_permission(hasPermission);

        var result = _subject.DoesContextHaveReadPermission(new() { ReadPermissions = new() { Rank = "otherRank" } });

        result.Should().Be(hasPermission);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void When_checking_permission_for_units_and_rank(bool hasUnitPermission, bool hasRankPermission, bool hasPermission)
    {
        Given_unit_read_permissions(hasUnitPermission);
        Given_rank_permission(hasRankPermission);

        var result = _subject.DoesContextHaveReadPermission(new() { ReadPermissions = new() { Units = new() { _unitId }, Rank = "otherRank" } });

        result.Should().Be(hasPermission);
    }

    private void Given_unit_write_permissions(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(hasPermission);
    }

    private void Given_unit_read_permissions(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.AnyChildHasMember(_unitId, _memberId)).Returns(hasPermission);
    }

    private void Given_unit_read_permissions_for_selected_units_only(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.HasMember(_unitId, _memberId)).Returns(hasPermission);
    }

    private void Given_rank_permission(bool hasPermission)
    {
        _mockIRanksService.Setup(x => x.IsSuperiorOrEqual(It.IsAny<string>(), It.IsAny<string>())).Returns(hasPermission);
    }
}
