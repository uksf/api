using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class HybridDocumentPermissionsServiceTests
{
    private readonly Mock<IDocumentPermissionsService> _mockLegacyService = new();
    private readonly Mock<IRoleBasedDocumentPermissionsService> _mockRoleBasedService = new();
    private readonly HybridDocumentPermissionsService _service;

    public HybridDocumentPermissionsServiceTests()
    {
        _service = new HybridDocumentPermissionsService(_mockLegacyService.Object, _mockRoleBasedService.Object);
    }

    [Fact]
    public void DoesContextHaveReadPermission_WithRoleBasedPermissions_ShouldUseRoleBasedService()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Rank = "Private" } }
        };

        _mockRoleBasedService.Setup(x => x.DoesContextHaveReadPermission(metadata)).Returns(true);

        // Act
        var result = _service.DoesContextHaveReadPermission(metadata);

        // Assert
        result.Should().BeTrue();
        _mockRoleBasedService.Verify(x => x.DoesContextHaveReadPermission(metadata), Times.Once);
        _mockLegacyService.Verify(x => x.DoesContextHaveReadPermission(It.IsAny<DomainMetadataWithPermissions>()), Times.Never);
    }

    [Fact]
    public void DoesContextHaveReadPermission_WithoutRoleBasedPermissions_ShouldUseLegacyService()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions(), // Empty
            ReadPermissions = new DocumentPermissions { Rank = "Private" }
        };

        _mockLegacyService.Setup(x => x.DoesContextHaveReadPermission(metadata)).Returns(true);

        // Act
        var result = _service.DoesContextHaveReadPermission(metadata);

        // Assert
        result.Should().BeTrue();
        _mockLegacyService.Verify(x => x.DoesContextHaveReadPermission(metadata), Times.Once);
        _mockRoleBasedService.Verify(x => x.DoesContextHaveReadPermission(It.IsAny<DomainMetadataWithPermissions>()), Times.Never);
    }

    [Fact]
    public void DoesContextHaveWritePermission_WithRoleBasedPermissions_ShouldUseRoleBasedService()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Collaborators = new PermissionRole { Units = [ObjectId.GenerateNewId().ToString()] } }
        };

        _mockRoleBasedService.Setup(x => x.DoesContextHaveWritePermission(metadata)).Returns(true);

        // Act
        var result = _service.DoesContextHaveWritePermission(metadata);

        // Assert
        result.Should().BeTrue();
        _mockRoleBasedService.Verify(x => x.DoesContextHaveWritePermission(metadata), Times.Once);
        _mockLegacyService.Verify(x => x.DoesContextHaveWritePermission(It.IsAny<DomainMetadataWithPermissions>()), Times.Never);
    }

    [Fact]
    public void DoesContextHaveWritePermission_WithoutRoleBasedPermissions_ShouldUseLegacyService()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions(), // Empty
            WritePermissions = new DocumentPermissions { Rank = "Private" }
        };

        _mockLegacyService.Setup(x => x.DoesContextHaveWritePermission(metadata)).Returns(false);

        // Act
        var result = _service.DoesContextHaveWritePermission(metadata);

        // Assert
        result.Should().BeFalse();
        _mockLegacyService.Verify(x => x.DoesContextHaveWritePermission(metadata), Times.Once);
        _mockRoleBasedService.Verify(x => x.DoesContextHaveWritePermission(It.IsAny<DomainMetadataWithPermissions>()), Times.Never);
    }

    [Fact]
    public void GetEffectivePermissions_WithRoleBasedPermissions_ShouldUseRoleBasedService()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Rank = "Corporal" } }
        };

        var expectedPermissions = new RoleBasedDocumentPermissions
        {
            Viewers = new PermissionRole { Rank = "Corporal" }, Collaborators = new PermissionRole { Rank = "Sergeant" }
        };

        _mockRoleBasedService.Setup(x => x.GetEffectivePermissions(metadata)).Returns(expectedPermissions);

        // Act
        var result = _service.GetEffectivePermissions(metadata);

        // Assert
        result.Should().BeEquivalentTo(expectedPermissions);
        _mockRoleBasedService.Verify(x => x.GetEffectivePermissions(metadata), Times.Once);
    }

    [Fact]
    public void GetEffectivePermissions_WithoutRoleBasedPermissions_ShouldConvertLegacyToRoleBased()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions(), // Empty
            ReadPermissions = new DocumentPermissions { Rank = "Private", Units = [ObjectId.GenerateNewId().ToString()] },
            WritePermissions = new DocumentPermissions { Rank = "Corporal", Units = [ObjectId.GenerateNewId().ToString()] }
        };

        // Act
        var result = _service.GetEffectivePermissions(metadata);

        // Assert
        result.Viewers.Rank.Should().Be("Private");
        result.Viewers.Units.Should().BeEquivalentTo(metadata.ReadPermissions.Units);
        result.Collaborators.Rank.Should().Be("Corporal");
        result.Collaborators.Units.Should().BeEquivalentTo(metadata.WritePermissions.Units);
        _mockRoleBasedService.Verify(x => x.GetEffectivePermissions(It.IsAny<DomainMetadataWithPermissions>()), Times.Never);
    }

    [Fact]
    public void GetInheritedPermissions_WithRoleBasedPermissions_ShouldUseRoleBasedService()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Rank = "Lance Corporal" } }
        };

        var expectedPermissions = new RoleBasedDocumentPermissions { Viewers = new PermissionRole { Rank = "Private" } };

        _mockRoleBasedService.Setup(x => x.GetInheritedPermissions(metadata)).Returns(expectedPermissions);

        // Act
        var result = _service.GetInheritedPermissions(metadata);

        // Assert
        result.Should().BeEquivalentTo(expectedPermissions);
        _mockRoleBasedService.Verify(x => x.GetInheritedPermissions(metadata), Times.Once);
    }

    [Fact]
    public void GetInheritedPermissions_WithoutRoleBasedPermissions_ShouldReturnEmpty()
    {
        // Arrange
        var metadata = new DomainDocumentFolderMetadata
        {
            Id = ObjectId.GenerateNewId().ToString(),
            RoleBasedPermissions = new RoleBasedDocumentPermissions(), // Empty
            ReadPermissions = new DocumentPermissions { Rank = "Private" }
        };

        // Act
        var result = _service.GetInheritedPermissions(metadata);

        // Assert
        result.Should().NotBeNull();
        result.Viewers.Should().NotBeNull();
        result.Collaborators.Should().NotBeNull();
        result.Viewers.Rank.Should().BeEmpty();
        result.Viewers.Units.Should().BeEmpty();
        result.Collaborators.Rank.Should().BeEmpty();
        result.Collaborators.Units.Should().BeEmpty();
        _mockRoleBasedService.Verify(x => x.GetInheritedPermissions(It.IsAny<DomainMetadataWithPermissions>()), Times.Never);
    }
}
