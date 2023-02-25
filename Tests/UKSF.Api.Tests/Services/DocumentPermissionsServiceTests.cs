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
    private readonly Mock<IHttpContextService> _mockIHttpContextService = new();
    private readonly Mock<IUnitsService> _mockIUnitsService = new();
    private readonly Mock<IRanksService> _mockIRanksService = new();
    private readonly Mock<IAccountService> _mockIAccountService = new();

    private readonly string _memberId = ObjectId.GenerateNewId().ToString();
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();
    private readonly DocumentPermissionsService _subject;

    public DocumentPermissionsServiceTests()
    {
        _mockIHttpContextService.Setup(x => x.GetUserId()).Returns(_memberId);
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(false);
        _mockIAccountService.Setup(x => x.GetUserAccount()).Returns(new DomainAccount { Rank = "rank" });
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(false);

        _subject = new(_mockIHttpContextService.Object, _mockIUnitsService.Object, _mockIRanksService.Object, _mockIAccountService.Object);
    }

    [Fact]
    public void When_checking_permission_for_empty()
    {
        var result = _subject.DoesContextHaveReadPermission(new());

        result.Should().Be(true);
    }

    [Fact]
    public void When_checking_permission_as_superadmin()
    {
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(true);

        var result = _subject.DoesContextHaveReadPermission(new());

        result.Should().Be(true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_checking_read_permission_for_units_only(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(hasPermission);

        var result = _subject.DoesContextHaveReadPermission(new() { Units = new() { _unitId } });

        result.Should().Be(hasPermission);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_checking_write_permission_for_units_only(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(hasPermission);

        var result = _subject.DoesContextHaveWritePermission(new() { Units = new() { _unitId } });

        result.Should().Be(hasPermission);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_checking_permission_for_rank_only(bool hasPermission)
    {
        _mockIRanksService.Setup(x => x.IsSuperiorOrEqual(It.IsAny<string>(), It.IsAny<string>())).Returns(hasPermission);

        var result = _subject.DoesContextHaveReadPermission(new() { Rank = "otherRank" });

        result.Should().Be(hasPermission);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void When_checking_permission_for_units_and_rank(bool hasUnitPermission, bool hasRankPermission, bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(hasUnitPermission);
        _mockIRanksService.Setup(x => x.IsSuperiorOrEqual(It.IsAny<string>(), It.IsAny<string>())).Returns(hasRankPermission);

        var result = _subject.DoesContextHaveReadPermission(new() { Units = new() { _unitId }, Rank = "otherRank" });

        result.Should().Be(hasPermission);
    }
}
