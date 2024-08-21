using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
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
    private readonly string _readUnitId = ObjectId.GenerateNewId().ToString();
    private readonly DocumentPermissionsService _subject;
    private readonly string _writeUnitId = ObjectId.GenerateNewId().ToString();

    public DocumentPermissionsServiceTests()
    {
        _mockIHttpContextService.Setup(x => x.GetUserId()).Returns(_memberId);
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(false);
        _mockIAccountService.Setup(x => x.GetUserAccount()).Returns(new DomainAccount { Rank = "rank" });

        _subject = new DocumentPermissionsService(
            _mockIHttpContextService.Object,
            _mockIUnitsService.Object,
            _mockIRanksService.Object,
            _mockIAccountService.Object
        );
    }

    [Fact]
    public void When_read_permissions_are_empty()
    {
        var result = _subject.DoesContextHaveReadPermission(new DomainMetadataWithPermissions());

        result.Should().Be(true);
    }

    [Fact]
    public void When_user_is_superadmin()
    {
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(true);

        var result = _subject.DoesContextHaveWritePermission(new DomainMetadataWithPermissions());

        result.Should().Be(true);
    }

    [Fact]
    public void When_write_permissions_are_empty()
    {
        var result = _subject.DoesContextHaveWritePermission(new DomainMetadataWithPermissions());

        result.Should().Be(false);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void When_checking_write_permissions(bool hasUnitPermission, bool hasRankPermission, bool hasPermission)
    {
        Given_unit_write_permissions(hasUnitPermission);
        Given_rank_permission(hasRankPermission);

        var result = _subject.DoesContextHaveWritePermission(
            new DomainMetadataWithPermissions
            {
                WritePermissions = new DocumentPermissions
                {
                    Units = [_writeUnitId],
                    Rank = "permissionsRank",
                    SelectedUnitsOnly = false
                }
            }
        );

        result.Should().Be(hasPermission);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    public void When_checking_partial_write_permissions(bool hasUnits, bool hasRank, bool selectedUnitsOnly, bool hasPermission)
    {
        Given_unit_write_permissions(true);
        Given_unit_write_permissions_for_selected_units_only(true);
        Given_rank_permission(true);

        var result = _subject.DoesContextHaveWritePermission(
            new DomainMetadataWithPermissions
            {
                WritePermissions = new DocumentPermissions
                {
                    Units = hasUnits ? [_writeUnitId] : [],
                    Rank = hasRank ? "permissionsRank" : string.Empty,
                    SelectedUnitsOnly = selectedUnitsOnly
                }
            }
        );

        result.Should().Be(hasPermission);
    }

    [Fact]
    public void When_checking_write_permissions_for_selected_units_only()
    {
        Given_unit_write_permissions(false);
        Given_unit_write_permissions_for_selected_units_only(true);

        var result = _subject.DoesContextHaveWritePermission(
            new DomainMetadataWithPermissions { WritePermissions = new DocumentPermissions { Units = [_writeUnitId], SelectedUnitsOnly = true } }
        );

        result.Should().Be(true);
        Units_tree_is_not_checked(_writeUnitId);
        Only_selected_units_are_checked(_writeUnitId);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void When_checking_read_permissions(bool hasUnitPermission, bool hasRankPermission, bool hasPermission)
    {
        Given_unit_read_permissions(hasUnitPermission);
        Given_rank_permission(hasRankPermission);

        var result = _subject.DoesContextHaveReadPermission(
            new DomainMetadataWithPermissions
            {
                ReadPermissions = new DocumentPermissions
                {
                    Units = [_readUnitId],
                    Rank = "permissionsRank",
                    SelectedUnitsOnly = false
                }
            }
        );

        result.Should().Be(hasPermission);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, true)]
    public void When_checking_partial_read_permissions(bool hasUnits, bool hasRank, bool selectedUnitsOnly, bool hasPermission)
    {
        Given_unit_read_permissions(true);
        Given_unit_read_permissions_for_selected_units_only(true);
        Given_rank_permission(true);

        var result = _subject.DoesContextHaveReadPermission(
            new DomainMetadataWithPermissions
            {
                ReadPermissions =
                {
                    Units = hasUnits ? [_readUnitId] : [],
                    Rank = hasRank ? "permissionsRank" : string.Empty,
                    SelectedUnitsOnly = selectedUnitsOnly
                }
            }
        );

        result.Should().Be(hasPermission);
    }

    [Fact]
    public void When_checking_read_permissions_for_selected_units_only()
    {
        Given_unit_read_permissions(false);
        Given_unit_read_permissions_for_selected_units_only(true);

        var result = _subject.DoesContextHaveReadPermission(
            new DomainMetadataWithPermissions { ReadPermissions = new DocumentPermissions { Units = [_readUnitId], SelectedUnitsOnly = true } }
        );

        result.Should().Be(true);
        Units_tree_is_not_checked(_readUnitId);
        Only_selected_units_are_checked(_readUnitId);
    }

    private void Given_unit_write_permissions(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_writeUnitId, _memberId)).Returns(hasPermission);
    }

    private void Given_unit_write_permissions_for_selected_units_only(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.HasMember(_writeUnitId, _memberId)).Returns(hasPermission);
    }

    private void Given_unit_read_permissions(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.AnyChildHasMember(_readUnitId, _memberId)).Returns(hasPermission);
    }

    private void Given_unit_read_permissions_for_selected_units_only(bool hasPermission)
    {
        _mockIUnitsService.Setup(x => x.HasMember(_readUnitId, _memberId)).Returns(hasPermission);
    }

    private void Given_rank_permission(bool hasPermission)
    {
        _mockIRanksService.Setup(x => x.IsSuperiorOrEqual(It.IsAny<string>(), It.IsAny<string>())).Returns(hasPermission);
    }

    private void Only_selected_units_are_checked(string unitId)
    {
        _mockIUnitsService.Verify(x => x.HasMember(unitId, _memberId), Times.Once);
    }

    private void Units_tree_is_not_checked(string unitId)
    {
        _mockIUnitsService.Verify(x => x.AnyParentHasMember(unitId, _memberId), Times.Never);
        _mockIUnitsService.Verify(x => x.AnyChildHasMember(unitId, _memberId), Times.Never);
    }
}
