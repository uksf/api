using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class RoleBasedDocumentPermissionsServiceTests
{
    private readonly string _memberId = ObjectId.GenerateNewId().ToString();
    private readonly Mock<IAccountService> _mockIAccountService = new();
    private readonly Mock<IHttpContextService> _mockIHttpContextService = new();
    private readonly Mock<IRanksService> _mockIRanksService = new();
    private readonly Mock<IUnitsService> _mockIUnitsService = new();
    private readonly Mock<IDocumentFolderMetadataContext> _mockDocumentFolderMetadataContext = new();
    private readonly RoleBasedDocumentPermissionsService _subject;
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();
    private readonly string _parentUnitId = ObjectId.GenerateNewId().ToString();
    private readonly string _childUnitId = ObjectId.GenerateNewId().ToString();

    public RoleBasedDocumentPermissionsServiceTests()
    {
        _mockIHttpContextService.Setup(x => x.GetUserId()).Returns(_memberId);
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(false);
        _mockIAccountService.Setup(x => x.GetUserAccount()).Returns(new DomainAccount { Rank = "userRank" });

        _subject = new RoleBasedDocumentPermissionsService(
            _mockIHttpContextService.Object,
            _mockIUnitsService.Object,
            _mockIRanksService.Object,
            _mockIAccountService.Object,
            _mockDocumentFolderMetadataContext.Object
        );
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserIsOwner_ShouldReturnTrue()
    {
        var metadata = new DomainDocumentMetadata { Owner = _memberId };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveWritePermission_WhenUserIsOwner_ShouldReturnTrue()
    {
        var metadata = new DomainDocumentMetadata { Owner = _memberId };

        var result = _subject.DoesContextHaveWritePermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserIsSuperadmin_ShouldReturnTrue()
    {
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(true);
        var metadata = new DomainDocumentMetadata();

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveWritePermission_WhenUserIsSuperadmin_ShouldReturnTrue()
    {
        _mockIHttpContextService.Setup(x => x.UserHasPermission(Permissions.Superadmin)).Returns(true);
        var metadata = new DomainDocumentMetadata();

        var result = _subject.DoesContextHaveWritePermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserIsViewer_ShouldReturnTrue()
    {
        GivenUserIsInChildUnit();
        GivenUserHasRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Units = [_unitId], Rank = "requiredRank" } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveWritePermission_WhenUserIsCollaborator_ShouldReturnTrue()
    {
        GivenUserIsInParentUnit();
        GivenUserHasRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Collaborators = new PermissionRole { Units = [_unitId], Rank = "requiredRank" } }
        };

        var result = _subject.DoesContextHaveWritePermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserIsCollaborator_ShouldReturnTrue()
    {
        GivenUserIsInParentUnit();
        GivenUserHasRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Collaborators = new PermissionRole { Units = [_unitId], Rank = "requiredRank" } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserLacksUnitPermission_ShouldReturnFalse()
    {
        GivenUserIsNotInAnyUnit();
        GivenUserHasRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Units = [_unitId], Rank = "requiredRank" } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeFalse();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserLacksRankPermission_ShouldReturnFalse()
    {
        GivenUserIsInChildUnit();
        GivenUserLacksRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Units = [_unitId], Rank = "requiredRank" } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true, true)] // Has unit and rank -> access
    [InlineData(true, false, false)] // Has unit but no rank -> no access  
    [InlineData(false, true, false)] // No unit but has rank -> no access
    [InlineData(false, false, false)] // No unit or rank -> no access
    public void DoesContextHaveReadPermission_WithViewerRole_ShouldRequireBothUnitAndRank(bool hasUnit, bool hasRank, bool expectedResult)
    {
        if (hasUnit) GivenUserIsInChildUnit();
        else GivenUserIsNotInAnyUnit();
        if (hasRank) GivenUserHasRankPermission();
        else GivenUserLacksRankPermission();

        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Units = [_unitId], Rank = "requiredRank" } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public void DoesContextHaveReadPermission_WithOnlyRankRequirement_ShouldGrantAccessBasedOnRank()
    {
        GivenUserHasRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Rank = "requiredRank" } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WithOnlyUnitRequirement_ShouldGrantAccessBasedOnUnit()
    {
        GivenUserIsInChildUnit();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Units = [_unitId] } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void GetEffectivePermissions_WithInheritedPermissions_ShouldMergeCorrectly()
    {
        var parentFolder = new DomainDocumentFolderMetadata
        {
            Id = "parent",
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole { Units = [_parentUnitId], Rank = "parentRank" },
                Collaborators = new PermissionRole { Units = [_parentUnitId] }
            }
        };

        var childFolder = new DomainDocumentFolderMetadata
        {
            Id = "child",
            Parent = "parent",
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole { Units = [_childUnitId] } // Override units, inherit rank
            }
        };

        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle("parent")).Returns(parentFolder);

        var result = _subject.GetEffectivePermissions(childFolder);

        result.Viewers.Units.Should().BeEquivalentTo([_childUnitId]); // Overridden
        result.Viewers.Rank.Should().Be(""); // Not inherited from parent since child has Viewers role defined
        result.Collaborators.Units.Should().BeEquivalentTo([_parentUnitId]); // Inherited
        result.Collaborators.Rank.Should().Be(""); // Inherited
    }

    [Fact]
    public void GetEffectivePermissions_WithNoLocalPermissions_ShouldInheritFromParent()
    {
        var parentFolder = new DomainDocumentFolderMetadata
        {
            Id = "parent",
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole { Units = [_parentUnitId], Rank = "parentRank" },
                Collaborators = new PermissionRole { Units = [_childUnitId], Rank = "collaboratorRank" }
            }
        };

        var childFolder = new DomainDocumentFolderMetadata
        {
            Id = "child",
            Parent = "parent",
            RoleBasedPermissions = new RoleBasedDocumentPermissions() // Empty permissions
        };

        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle("parent")).Returns(parentFolder);

        var result = _subject.GetEffectivePermissions(childFolder);

        result.Viewers.Units.Should().BeEquivalentTo([_parentUnitId]);
        result.Viewers.Rank.Should().Be("parentRank");
        result.Collaborators.Units.Should().BeEquivalentTo([_childUnitId]);
        result.Collaborators.Rank.Should().Be("collaboratorRank");
    }

    [Fact]
    public void GetInheritedPermissions_ShouldTraverseParentHierarchyUntilPermissionsFound()
    {
        // Arrange: Create a hierarchy: Document A -> Folder B -> Folder C -> Folder D
        // Only Folder D has permissions set, so Document A should inherit from Folder D

        var folderDId = ObjectId.GenerateNewId().ToString();
        var folderCId = ObjectId.GenerateNewId().ToString();
        var folderBId = ObjectId.GenerateNewId().ToString();
        var documentAId = ObjectId.GenerateNewId().ToString();

        // Folder D (has permissions - top of hierarchy with permissions)
        var folderD = new DomainDocumentFolderMetadata
        {
            Id = folderDId,
            Parent = null, // Root folder
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole { Rank = "Sergeant" }, Collaborators = new PermissionRole { Rank = "Staff Sergeant" }
            }
        };

        // Folder C (no permissions - should traverse to parent)
        var folderC = new DomainDocumentFolderMetadata
        {
            Id = folderCId,
            Parent = folderDId,
            RoleBasedPermissions = new RoleBasedDocumentPermissions() // Empty permissions
        };

        // Folder B (no permissions - should traverse to parent)
        var folderB = new DomainDocumentFolderMetadata
        {
            Id = folderBId,
            Parent = folderCId,
            RoleBasedPermissions = new RoleBasedDocumentPermissions() // Empty permissions
        };

        // Document A (in Folder B)
        var documentA = new DomainDocumentMetadata { Id = documentAId, Folder = folderBId };

        // Setup mocks to return the folder hierarchy
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderBId)).Returns(folderB);
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderCId)).Returns(folderC);
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderDId)).Returns(folderD);

        // Act
        var result = _subject.GetInheritedPermissions(documentA);

        // Assert
        result.Should().NotBeNull();
        result.Viewers.Rank.Should().Be("Sergeant");
        result.Collaborators.Rank.Should().Be("Staff Sergeant");

        // Verify the context was called for each level of the hierarchy
        // Note: Each folder is now called twice (once for viewers, once for collaborators)
        _mockDocumentFolderMetadataContext.Verify(x => x.GetSingle(folderBId), Times.Exactly(2));
        _mockDocumentFolderMetadataContext.Verify(x => x.GetSingle(folderCId), Times.Exactly(2));
        _mockDocumentFolderMetadataContext.Verify(x => x.GetSingle(folderDId), Times.Exactly(2));
    }

    [Fact]
    public void GetInheritedPermissions_ShouldReturnEmptyWhenNoPermissionsFoundInHierarchy()
    {
        // Arrange: Create a hierarchy where no folders have permissions
        var folderCId = ObjectId.GenerateNewId().ToString();
        var folderBId = ObjectId.GenerateNewId().ToString();
        var documentAId = ObjectId.GenerateNewId().ToString();

        // Folder C (no permissions - root folder)
        var folderC = new DomainDocumentFolderMetadata
        {
            Id = folderCId,
            Parent = null, // Root folder
            RoleBasedPermissions = new RoleBasedDocumentPermissions() // Empty permissions
        };

        // Folder B (no permissions)
        var folderB = new DomainDocumentFolderMetadata
        {
            Id = folderBId,
            Parent = folderCId,
            RoleBasedPermissions = new RoleBasedDocumentPermissions() // Empty permissions
        };

        // Document A (in Folder B)
        var documentA = new DomainDocumentMetadata { Id = documentAId, Folder = folderBId };

        // Setup mocks to return the folder hierarchy
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderBId)).Returns(folderB);
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderCId)).Returns(folderC);

        // Act
        var result = _subject.GetInheritedPermissions(documentA);

        // Assert
        result.Should().NotBeNull();
        result.Viewers.Should().NotBeNull();
        result.Viewers.Units.Should().BeEmpty();
        result.Viewers.Rank.Should().BeEmpty();
        result.Collaborators.Should().NotBeNull();
        result.Collaborators.Units.Should().BeEmpty();
        result.Collaborators.Rank.Should().BeEmpty();

        // Verify the context was called for each level of the hierarchy
        // Note: Each folder is now called twice (once for viewers, once for collaborators)
        _mockDocumentFolderMetadataContext.Verify(x => x.GetSingle(folderBId), Times.Exactly(2));
        _mockDocumentFolderMetadataContext.Verify(x => x.GetSingle(folderCId), Times.Exactly(2));
    }

    [Theory]
    [InlineData(true, true, true)] // ExpandToSubUnits=true, viewer role -> checks child units (current behavior)
    [InlineData(false, true, false)] // ExpandToSubUnits=false, viewer role -> checks only selected units
    public void DoesContextHaveReadPermission_WithExpandToSubUnitsFlag_ShouldRespectFlagForViewers(bool expandToSubUnits, bool userInChild, bool expectedResult)
    {
        // Arrange
        if (userInChild) GivenUserIsInChildUnit();
        else GivenUserIsNotInAnyUnit();
        GivenUserHasRankPermission();

        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Units = [_unitId],
                    Rank = "requiredRank",
                    ExpandToSubUnits = expandToSubUnits
                }
            }
        };

        // Act
        var result = _subject.DoesContextHaveReadPermission(metadata);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(true, true, true, true)] // ExpandToSubUnits=true, user in parent -> has access
    [InlineData(true, false, true, true)] // ExpandToSubUnits=true, user in child -> has access
    [InlineData(false, true, false, false)] // ExpandToSubUnits=false, user in parent -> no access
    [InlineData(false, false, true, false)] // ExpandToSubUnits=false, user in child -> no access
    public void DoesContextHaveWritePermission_WithExpandToSubUnitsFlag_ShouldRespectFlagForCollaborators(
        bool expandToSubUnits,
        bool userInParent,
        bool userInChild,
        bool expectedResult
    )
    {
        // Arrange
        if (userInParent) GivenUserIsInParentUnit();
        if (userInChild) GivenUserIsInChildUnit();
        if (!userInParent && !userInChild) GivenUserIsNotInAnyUnit();
        GivenUserHasRankPermission();

        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Collaborators = new PermissionRole
                {
                    Units = [_unitId],
                    Rank = "requiredRank",
                    ExpandToSubUnits = expandToSubUnits
                }
            }
        };

        // Act
        var result = _subject.DoesContextHaveWritePermission(metadata);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void DoesContextHaveReadPermission_WithExpandToSubUnitsFalse_ShouldCheckOnlySelectedUnits()
    {
        // Arrange: User is a direct member of the selected unit
        _mockIUnitsService.Setup(x => x.HasMember(_unitId, _memberId)).Returns(true);
        _mockIUnitsService.Setup(x => x.AnyChildHasMember(_unitId, _memberId)).Returns(false);
        GivenUserHasRankPermission();

        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Units = [_unitId],
                    Rank = "requiredRank",
                    ExpandToSubUnits = false
                }
            }
        };

        // Act
        var result = _subject.DoesContextHaveReadPermission(metadata);

        // Assert
        result.Should().BeTrue();
        _mockIUnitsService.Verify(x => x.HasMember(_unitId, _memberId), Times.Once);
        _mockIUnitsService.Verify(x => x.AnyChildHasMember(_unitId, _memberId), Times.Never);
    }

    [Fact]
    public void DoesContextHaveWritePermission_WithExpandToSubUnitsFalse_ShouldCheckOnlySelectedUnits()
    {
        // Arrange: User is a direct member of the selected unit
        _mockIUnitsService.Setup(x => x.HasMember(_unitId, _memberId)).Returns(true);
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(false);
        _mockIUnitsService.Setup(x => x.AnyChildHasMember(_unitId, _memberId)).Returns(false);
        GivenUserHasRankPermission();

        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Collaborators = new PermissionRole
                {
                    Units = [_unitId],
                    Rank = "requiredRank",
                    ExpandToSubUnits = false
                }
            }
        };

        // Act
        var result = _subject.DoesContextHaveWritePermission(metadata);

        // Assert
        result.Should().BeTrue();
        _mockIUnitsService.Verify(x => x.HasMember(_unitId, _memberId), Times.Once);
        _mockIUnitsService.Verify(x => x.AnyParentHasMember(_unitId, _memberId), Times.Never);
        _mockIUnitsService.Verify(x => x.AnyChildHasMember(_unitId, _memberId), Times.Never);
    }

    [Fact]
    public void GetEffectivePermissions_ShouldIncludeExpandToSubUnitsFlag()
    {
        // Arrange
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Units = [_unitId],
                    Rank = "testRank",
                    ExpandToSubUnits = false
                },
                Collaborators = new PermissionRole { Units = [_childUnitId], ExpandToSubUnits = true }
            }
        };

        // Act
        var result = _subject.GetEffectivePermissions(metadata);

        // Assert
        result.Viewers.ExpandToSubUnits.Should().BeFalse();
        result.Collaborators.ExpandToSubUnits.Should().BeTrue();
    }

    [Fact]
    public void GetInheritedPermissions_ShouldInheritViewersAndCollaboratorsIndependently()
    {
        // Arrange: Create a hierarchy where viewers and collaborators inherit from different levels
        // Document A -> Folder B (custom viewers, no collaborators) -> Folder C (no viewers, custom collaborators) -> Folder D (root)

        var folderDId = ObjectId.GenerateNewId().ToString();
        var folderCId = ObjectId.GenerateNewId().ToString();
        var folderBId = ObjectId.GenerateNewId().ToString();
        var documentAId = ObjectId.GenerateNewId().ToString();

        // Folder D (root - no permissions)
        var folderD = new DomainDocumentFolderMetadata
        {
            Id = folderDId,
            Parent = null,
            RoleBasedPermissions = new RoleBasedDocumentPermissions()
        };

        // Folder C (has collaborator permissions only)
        var folderC = new DomainDocumentFolderMetadata
        {
            Id = folderCId,
            Parent = folderDId,
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Collaborators = new PermissionRole { Rank = "Staff Sergeant" } }
        };

        // Folder B (has viewer permissions only)
        var folderB = new DomainDocumentFolderMetadata
        {
            Id = folderBId,
            Parent = folderCId,
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Rank = "Sergeant" } }
        };

        // Document A (in Folder B)
        var documentA = new DomainDocumentMetadata { Id = documentAId, Folder = folderBId };

        // Setup mocks
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderBId)).Returns(folderB);
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderCId)).Returns(folderC);
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(folderDId)).Returns(folderD);

        // Act
        var result = _subject.GetInheritedPermissions(documentA);

        // Assert
        result.Should().NotBeNull();
        result.Viewers.Rank.Should().Be("Sergeant"); // Inherited from Folder B
        result.Collaborators.Rank.Should().Be("Staff Sergeant"); // Inherited from Folder C
    }

    [Fact]
    public void GetInheritedPermissions_WhenOnlyViewersDefinedInHierarchy_ShouldInheritOnlyViewers()
    {
        // Arrange: Create a hierarchy where only viewers are defined at any level
        var parentId = ObjectId.GenerateNewId().ToString();
        var childId = ObjectId.GenerateNewId().ToString();
        var documentId = ObjectId.GenerateNewId().ToString();

        var parentFolder = new DomainDocumentFolderMetadata
        {
            Id = parentId,
            Parent = null,
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole { Rank = "Corporal" }
                // No collaborators defined
            }
        };

        var childFolder = new DomainDocumentFolderMetadata
        {
            Id = childId,
            Parent = parentId,
            RoleBasedPermissions = new RoleBasedDocumentPermissions()
        };

        var document = new DomainDocumentMetadata { Id = documentId, Folder = childId };

        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(childId)).Returns(childFolder);
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(parentId)).Returns(parentFolder);

        // Act
        var result = _subject.GetInheritedPermissions(document);

        // Assert
        result.Should().NotBeNull();
        result.Viewers.Rank.Should().Be("Corporal");
        result.Collaborators.Should().NotBeNull();
        result.Collaborators.Units.Should().BeEmpty();
        result.Collaborators.Rank.Should().BeEmpty();
    }

    [Fact]
    public void GetInheritedPermissions_WhenOnlyCollaboratorsDefinedInHierarchy_ShouldInheritOnlyCollaborators()
    {
        // Arrange: Create a hierarchy where only collaborators are defined at any level
        var parentId = ObjectId.GenerateNewId().ToString();
        var childId = ObjectId.GenerateNewId().ToString();
        var documentId = ObjectId.GenerateNewId().ToString();

        var parentFolder = new DomainDocumentFolderMetadata
        {
            Id = parentId,
            Parent = null,
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                // No viewers defined
                Collaborators = new PermissionRole { Rank = "Lieutenant" }
            }
        };

        var childFolder = new DomainDocumentFolderMetadata
        {
            Id = childId,
            Parent = parentId,
            RoleBasedPermissions = new RoleBasedDocumentPermissions()
        };

        var document = new DomainDocumentMetadata { Id = documentId, Folder = childId };

        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(childId)).Returns(childFolder);
        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle(parentId)).Returns(parentFolder);

        // Act
        var result = _subject.GetInheritedPermissions(document);

        // Assert
        result.Should().NotBeNull();
        result.Viewers.Should().NotBeNull();
        result.Viewers.Units.Should().BeEmpty();
        result.Viewers.Rank.Should().BeEmpty();
        result.Collaborators.Rank.Should().Be("Lieutenant");
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserIsInUsersList_ShouldReturnTrue()
    {
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Users = [_memberId] } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveWritePermission_WhenUserIsInCollaboratorUsersList_ShouldReturnTrue()
    {
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Collaborators = new PermissionRole { Users = [_memberId] } }
        };

        var result = _subject.DoesContextHaveWritePermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserIsInUsersListButLacksUnitAndRank_ShouldReturnTrue()
    {
        GivenUserIsNotInAnyUnit();
        GivenUserLacksRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Users = [_memberId],
                    Units = [_unitId],
                    Rank = "requiredRank"
                }
            }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserNotInUsersListButHasUnitAndRank_ShouldReturnTrue()
    {
        var otherUserId = ObjectId.GenerateNewId().ToString();
        GivenUserIsInChildUnit();
        GivenUserHasRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Users = [otherUserId], // Different user
                    Units = [_unitId],
                    Rank = "requiredRank"
                }
            }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserNotInUsersListAndLacksUnitButHasRank_ShouldReturnFalse()
    {
        var otherUserId = ObjectId.GenerateNewId().ToString();
        GivenUserIsNotInAnyUnit();
        GivenUserHasRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Users = [otherUserId], // Different user
                    Units = [_unitId],
                    Rank = "requiredRank"
                }
            }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeFalse();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenUserNotInUsersListAndHasUnitButLacksRank_ShouldReturnFalse()
    {
        var otherUserId = ObjectId.GenerateNewId().ToString();
        GivenUserIsInChildUnit();
        GivenUserLacksRankPermission();
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Users = [otherUserId], // Different user
                    Units = [_unitId],
                    Rank = "requiredRank"
                }
            }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeFalse();
    }

    [Fact]
    public void DoesContextHaveReadPermission_WhenOnlyUsersListSpecified_ShouldWorkCorrectly()
    {
        var metadata = new DomainDocumentMetadata
        {
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Users = [_memberId] } }
        };

        var result = _subject.DoesContextHaveReadPermission(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void GetEffectivePermissions_WithInheritedUserPermissions_ShouldInheritUsersCorrectly()
    {
        var otherUserId = ObjectId.GenerateNewId().ToString();
        var parentFolder = new DomainDocumentFolderMetadata
        {
            Id = "parent",
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole
                {
                    Users = [_memberId],
                    Units = [_parentUnitId],
                    Rank = "parentRank"
                },
                Collaborators = new PermissionRole { Users = [otherUserId], Units = [_parentUnitId] }
            }
        };

        var childFolder = new DomainDocumentFolderMetadata
        {
            Id = "child",
            Parent = "parent",
            RoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole { Units = [_childUnitId] } // Completely replaces inherited Viewers role
            }
        };

        _mockDocumentFolderMetadataContext.Setup(x => x.GetSingle("parent")).Returns(parentFolder);

        var result = _subject.GetEffectivePermissions(childFolder);

        result.Viewers.Units.Should().BeEquivalentTo([_childUnitId]); // Completely replaced
        result.Viewers.Users.Should().BeEmpty(); // Completely replaced (child has no users)
        result.Viewers.Rank.Should().Be(""); // Completely replaced (child has no rank)
        result.Collaborators.Units.Should().BeEquivalentTo([_parentUnitId]); // Inherited
        result.Collaborators.Users.Should().BeEquivalentTo([otherUserId]); // Inherited
        result.Collaborators.Rank.Should().Be(""); // Inherited
    }

    [Fact]
    public void ClonePermissionRole_WithUsers_ShouldCloneUsersCorrectly()
    {
        var otherUserId = ObjectId.GenerateNewId().ToString();
        var originalRole = new PermissionRole
        {
            Units = [_unitId],
            Users = [_memberId, otherUserId],
            Rank = "TestRank",
            ExpandToSubUnits = false
        };

        var clonedRole = CallClonePermissionRole(originalRole);

        clonedRole.Should().NotBeSameAs(originalRole);
        clonedRole.Units.Should().BeEquivalentTo(originalRole.Units);
        clonedRole.Users.Should().BeEquivalentTo(originalRole.Users);
        clonedRole.Rank.Should().Be(originalRole.Rank);
        clonedRole.ExpandToSubUnits.Should().Be(originalRole.ExpandToSubUnits);

        // Verify deep cloning
        clonedRole.Users.Should().NotBeSameAs(originalRole.Users);
        clonedRole.Units.Should().NotBeSameAs(originalRole.Units);
    }

    // Helper method to access private ClonePermissionRole method
    private static PermissionRole CallClonePermissionRole(PermissionRole role)
    {
        var method = typeof(RoleBasedDocumentPermissionsService).GetMethod(
            "ClonePermissionRole",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );
        return (PermissionRole)method.Invoke(null, [role]);
    }

    private void GivenUserIsInChildUnit()
    {
        _mockIUnitsService.Setup(x => x.AnyChildHasMember(_unitId, _memberId)).Returns(true);
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(false);
        _mockIUnitsService.Setup(x => x.HasMember(_unitId, _memberId)).Returns(false);
    }

    private void GivenUserIsInParentUnit()
    {
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(true);
        _mockIUnitsService.Setup(x => x.AnyChildHasMember(_unitId, _memberId)).Returns(false);
        _mockIUnitsService.Setup(x => x.HasMember(_unitId, _memberId)).Returns(false);
    }

    private void GivenUserIsNotInAnyUnit()
    {
        _mockIUnitsService.Setup(x => x.AnyChildHasMember(_unitId, _memberId)).Returns(false);
        _mockIUnitsService.Setup(x => x.AnyParentHasMember(_unitId, _memberId)).Returns(false);
        _mockIUnitsService.Setup(x => x.HasMember(_unitId, _memberId)).Returns(false);
    }

    private void GivenUserHasRankPermission()
    {
        _mockIRanksService.Setup(x => x.IsSuperiorOrEqual("userRank", "requiredRank")).Returns(true);
    }

    private void GivenUserLacksRankPermission()
    {
        _mockIRanksService.Setup(x => x.IsSuperiorOrEqual("userRank", "requiredRank")).Returns(false);
    }
}
