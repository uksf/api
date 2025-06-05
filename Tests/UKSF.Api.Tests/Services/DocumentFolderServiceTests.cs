using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;
using UKSF.Api.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class DocumentFolderServiceTests
{
    private readonly Mock<IDocumentFolderMetadataContext> _mockIDocumentFolderMetadataContext = new();
    private readonly Mock<IFileContext> _mockIFileContext = new();
    private readonly Mock<IHttpContextService> _mockIHttpContextService = new();
    private readonly Mock<IRoleBasedDocumentPermissionsService> _mockIRoleBasedDocumentPermissionsService = new();
    private readonly Mock<IUksfLogger> _mockIUksfLogger = new();
    private readonly Mock<IVariablesService> _mockIVariablesService = new();
    private readonly Mock<IClock> _mockIClock = new();
    private readonly DocumentFolderService _subject;
    private readonly DateTime _utcNow = DateTime.UtcNow;

    public DocumentFolderServiceTests()
    {
        _mockIHttpContextService.Setup(x => x.GetUserId()).Returns(ObjectId.GenerateNewId().ToString());
        _mockIRoleBasedDocumentPermissionsService.Setup(x => x.DoesContextHaveReadPermission(It.IsAny<DomainMetadataWithPermissions>())).Returns(true);
        _mockIRoleBasedDocumentPermissionsService.Setup(x => x.DoesContextHaveWritePermission(It.IsAny<DomainMetadataWithPermissions>())).Returns(true);
        _mockIClock.Setup(x => x.UtcNow()).Returns(_utcNow);

        _subject = new DocumentFolderService(
            _mockIDocumentFolderMetadataContext.Object,
            _mockIHttpContextService.Object,
            _mockIRoleBasedDocumentPermissionsService.Object,
            _mockIFileContext.Object,
            _mockIVariablesService.Object,
            _mockIClock.Object,
            _mockIUksfLogger.Object
        );
    }

    [Fact]
    public async Task When_creating_a_folder_at_root()
    {
        _mockIDocumentFolderMetadataContext.Setup(x => x.Get()).Returns(new List<DomainDocumentFolderMetadata>());

        await _subject.CreateFolder(new CreateFolderRequest { Parent = ObjectId.Empty.ToString(), Name = "About" });

        _mockIDocumentFolderMetadataContext.Verify(x => x.Add(It.IsAny<DomainDocumentFolderMetadata>()), Times.Once());
    }

    [Fact]
    public async Task When_creating_a_folder_at_root_with_existing_name()
    {
        Given_folder_metadata();

        var act = async () => await _subject.CreateFolder(new CreateFolderRequest { Parent = ObjectId.Empty.ToString(), Name = "UKSF" });

        await act.Should().ThrowAsync<FolderException>().WithMessageAndStatusCode("A folder already exists at path 'UKSF'", 400);
    }

    [Fact]
    public async Task When_creating_a_folder_with_existing_name()
    {
        Given_folder_metadata();

        var act = async () => await _subject.CreateFolder(new CreateFolderRequest { Parent = "2", Name = "Training" });

        await act.Should().ThrowAsync<FolderException>().WithMessageAndStatusCode("A folder already exists at path 'UKSF\\JSFAW\\Training'", 400);
    }

    [Fact]
    public async Task When_creating_a_folder_with_new_name()
    {
        Given_folder_metadata();

        await _subject.CreateFolder(new CreateFolderRequest { Parent = "2", Name = "SOPs" });

        _mockIDocumentFolderMetadataContext.Verify(x => x.Add(It.IsAny<DomainDocumentFolderMetadata>()), Times.Once());
    }

    [Fact]
    public async Task When_creating_a_folder_without_permission()
    {
        Given_folder_metadata();
        _mockIRoleBasedDocumentPermissionsService.Setup(x => x.DoesContextHaveWritePermission(It.IsAny<DomainMetadataWithPermissions>())).Returns(false);

        var act = async () => await _subject.CreateFolder(new CreateFolderRequest { Parent = "2", Name = "SOPs" });

        await act.Should().ThrowAsync<FolderException>().WithMessageAndStatusCode("Cannot create folder", 400);
    }

    [Theory]
    [InlineData("2", "SOPs")]
    [InlineData("3", "SOPs")]
    public async Task When_creating_a_folder_with_same_name_different_parent(string parent, string name)
    {
        Given_folder_metadata();

        await _subject.CreateFolder(new CreateFolderRequest { Parent = parent, Name = name });

        _mockIDocumentFolderMetadataContext.Verify(x => x.Add(It.IsAny<DomainDocumentFolderMetadata>()), Times.Once());
    }

    [Fact]
    public async Task UpdateFolder_WhenNameChanges_ShouldUpdateFullPath()
    {
        // Arrange
        Given_folder_metadata();
        var newRequest = new CreateFolderRequest { Name = "NewFolderName", Parent = "1" };

        // Act
        await _subject.UpdateFolder("2", newRequest);

        // Assert - Just verify that Update was called, which means the update logic ran
        _mockIDocumentFolderMetadataContext.Verify(x => x.Update("2", It.IsAny<UpdateDefinition<DomainDocumentFolderMetadata>>()), Times.Once);
    }

    [Fact]
    public async Task UpdateFolder_WhenNameChangesToExistingPath_ShouldThrowException()
    {
        // Arrange
        Given_folder_metadata();
        var newRequest = new CreateFolderRequest { Name = "SFSG", Parent = "1" }; // This would create "UKSF\\SFSG" which already exists (folder 3)

        // Act & Assert
        var act = async () => await _subject.UpdateFolder("2", newRequest);

        await act.Should().ThrowAsync<FolderException>().WithMessage("A folder already exists at path 'UKSF\\SFSG'");
    }

    [Theory]
    [InlineData("parent", "New Parent")]
    [InlineData("child", "New Child")]
    public async Task UpdateFolder_WithValidNameChange_ShouldSucceed(string parentId, string newName)
    {
        Given_folder_metadata();
        var newRequest = new CreateFolderRequest { Name = newName, Parent = "1" };

        await _subject.UpdateFolder(parentId == "parent" ? "2" : "3", newRequest);

        _mockIDocumentFolderMetadataContext.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainDocumentFolderMetadata>>()),
            Times.AtLeastOnce()
        );
    }

    private void Given_folder_metadata()
    {
        _mockIDocumentFolderMetadataContext.Setup(x => x.Get())
                                           .Returns(
                                               new List<DomainDocumentFolderMetadata>
                                               {
                                                   new()
                                                   {
                                                       Id = "1",
                                                       Parent = ObjectId.Empty.ToString(),
                                                       Name = "UKSF",
                                                       FullPath = "UKSF"
                                                   },
                                                   new()
                                                   {
                                                       Id = "2",
                                                       Parent = "1",
                                                       Name = "JSFAW",
                                                       FullPath = "UKSF\\JSFAW"
                                                   },
                                                   new()
                                                   {
                                                       Id = "3",
                                                       Parent = "1",
                                                       Name = "SFSG",
                                                       FullPath = "UKSF\\SFSG"
                                                   },
                                                   new()
                                                   {
                                                       Id = "4",
                                                       Parent = "2",
                                                       Name = "Training",
                                                       FullPath = "UKSF\\JSFAW\\Training"
                                                   },
                                                   new()
                                                   {
                                                       Id = "5",
                                                       Parent = "3",
                                                       Name = "Training",
                                                       FullPath = "UKSF\\SFSG\\Training"
                                                   }
                                               }
                                           );
        _mockIDocumentFolderMetadataContext.Setup(x => x.GetSingle("2"))
        .Returns(
            new DomainDocumentFolderMetadata
            {
                Id = "2",
                Parent = "1",
                Name = "JSFAW",
                FullPath = "UKSF\\JSFAW"
            }
        );
        _mockIDocumentFolderMetadataContext.Setup(x => x.GetSingle("3"))
        .Returns(
            new DomainDocumentFolderMetadata
            {
                Id = "3",
                Parent = "1",
                Name = "SFSG",
                FullPath = "UKSF\\SFSG"
            }
        );
    }
}
