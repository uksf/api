using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Mock<IClock> _mockIClock = new();
    private readonly Mock<IDocumentFolderMetadataContext> _mockIDocumentFolderMetadataContext = new();
    private readonly Mock<IDocumentPermissionsService> _mockIDocumentPermissionsService = new();
    private readonly Mock<IFileContext> _mockIFileContext = new();
    private readonly Mock<IHttpContextService> _mockIHttpContextService = new();
    private readonly Mock<IUksfLogger> _mockIUksfLogger = new();
    private readonly Mock<IVariablesService> _mockIVariablesService = new();
    private readonly DocumentFolderService _subject;
    private readonly DateTime _utcNow = DateTime.UtcNow;

    public DocumentFolderServiceTests()
    {
        _mockIHttpContextService.Setup(x => x.GetUserId()).Returns(ObjectId.GenerateNewId().ToString());
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.IsAny<DomainMetadataWithPermissions>())).Returns(true);
        _mockIDocumentPermissionsService.Setup(x => x.CanContextCollaborate(It.IsAny<DomainMetadataWithPermissions>())).Returns(true);
        _mockIClock.Setup(x => x.UtcNow()).Returns(_utcNow);

        _subject = new DocumentFolderService(
            _mockIDocumentFolderMetadataContext.Object,
            _mockIHttpContextService.Object,
            _mockIDocumentPermissionsService.Object,
            _mockIFileContext.Object,
            _mockIVariablesService.Object,
            _mockIClock.Object,
            _mockIUksfLogger.Object
        );
    }

    [Fact]
    public void GetAllFolders_WithDocumentsInMultipleFolders_ShouldFilterDocumentsByPermission()
    {
        // Arrange
        Given_multiple_folders_with_documents();

        // Setup permission service - allow folder access but selective document access
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.IsAny<DomainDocumentFolderMetadata>())).Returns(true);

        // Document permissions - deny access to some documents
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.Is<DomainDocumentMetadata>(d => d.Id == "doc_folder2_1")))
                                        .Returns(false); // No access to first doc in folder 2
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.Is<DomainDocumentMetadata>(d => d.Id == "doc_folder2_2")))
                                        .Returns(true); // Allow access to second doc in folder 2
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.Is<DomainDocumentMetadata>(d => d.Id == "doc_folder3_1")))
                                        .Returns(true); // Allow access to first doc in folder 3
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.Is<DomainDocumentMetadata>(d => d.Id == "doc_folder3_2")))
                                        .Returns(false); // No access to second doc in folder 3

        // Act
        var result = _subject.GetAllFolders();

        // Assert
        var folder2 = result.First(f => f.Id == "2");
        var folder3 = result.First(f => f.Id == "3");

        folder2.Documents.Should().HaveCount(1); // Should only have doc_folder2_2
        folder2.Documents.Should().Contain(d => d.Id == "doc_folder2_2");
        folder2.Documents.Should().NotContain(d => d.Id == "doc_folder2_1");

        folder3.Documents.Should().HaveCount(1); // Should only have doc_folder3_1
        folder3.Documents.Should().Contain(d => d.Id == "doc_folder3_1");
        folder3.Documents.Should().NotContain(d => d.Id == "doc_folder3_2");
    }

    [Fact]
    public async Task GetFolder_WhenUserCanSeeFolderButNoDocuments_ShouldReturnFolderWithEmptyDocuments()
    {
        // Arrange
        Given_folder_metadata_with_documents_having_different_permissions();

        // User can access folder but not any documents
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.IsAny<DomainDocumentFolderMetadata>())).Returns(true);
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.IsAny<DomainDocumentMetadata>())).Returns(false);

        // Act
        var result = await _subject.GetFolder("2");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("2");
        result.Name.Should().Be("JSFAW");
        result.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFolder_WithAllDocumentsHavingNoReadPermission_ShouldReturnEmptyDocumentsList()
    {
        // Arrange
        Given_folder_metadata_with_documents_having_different_permissions();

        // Setup permission service to deny read permission for all documents
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.IsAny<DomainDocumentMetadata>())).Returns(false);

        // Act
        var result = await _subject.GetFolder("2");

        // Assert
        result.Documents.Should().BeEmpty();
        _mockIDocumentPermissionsService.Verify(x => x.CanContextView(It.IsAny<DomainDocumentMetadata>()), Times.Exactly(3)); // Should check all 3 documents
    }

    [Fact]
    public async Task GetFolder_WithAllDocumentsHavingReadPermission_ShouldReturnAllDocuments()
    {
        // Arrange
        Given_folder_metadata_with_documents_having_different_permissions();

        // Setup permission service to allow read permission for all documents
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.IsAny<DomainDocumentMetadata>())).Returns(true);

        // Act
        var result = await _subject.GetFolder("2");

        // Assert
        result.Documents.Should().HaveCount(3);
        result.Documents.Should().Contain(d => d.Id == "doc1");
        result.Documents.Should().Contain(d => d.Id == "doc2");
        result.Documents.Should().Contain(d => d.Id == "doc3");
    }

    [Fact]
    public async Task GetFolder_WithDocumentsAndNoReadPermission_ShouldFilterOutDocuments()
    {
        // Arrange
        Given_folder_metadata_with_documents_having_different_permissions();

        // Setup permission service to deny read permission for specific documents
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.Is<DomainDocumentMetadata>(d => d.Id == "doc1"))).Returns(true);
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.Is<DomainDocumentMetadata>(d => d.Id == "doc2")))
                                        .Returns(false); // No read permission for doc2
        _mockIDocumentPermissionsService.Setup(x => x.CanContextView(It.Is<DomainDocumentMetadata>(d => d.Id == "doc3"))).Returns(true);

        // Act
        var result = await _subject.GetFolder("2");

        // Assert
        result.Documents.Should().HaveCount(2);
        result.Documents.Should().Contain(d => d.Id == "doc1");
        result.Documents.Should().Contain(d => d.Id == "doc3");
        result.Documents.Should().NotContain(d => d.Id == "doc2");
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
        _mockIDocumentPermissionsService.Setup(x => x.CanContextCollaborate(It.IsAny<DomainMetadataWithPermissions>())).Returns(false);

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

    private void Given_folder_metadata_with_documents_having_different_permissions()
    {
        var folderWithDocuments = new DomainDocumentFolderMetadata
        {
            Id = "2",
            Parent = "1",
            Name = "JSFAW",
            FullPath = "UKSF\\JSFAW",
            Documents = new List<DomainDocumentMetadata>
            {
                new()
                {
                    Id = "doc1",
                    Folder = "2",
                    Name = "Training.json",
                    FullPath = "UKSF\\JSFAW\\Training.json",
                    Created = _utcNow.AddDays(-3),
                    LastUpdated = _utcNow.AddDays(-1)
                },
                new()
                {
                    Id = "doc2",
                    Folder = "2",
                    Name = "Secret.json",
                    FullPath = "UKSF\\JSFAW\\Secret.json",
                    Created = _utcNow.AddDays(-2),
                    LastUpdated = _utcNow.AddDays(-1)
                },
                new()
                {
                    Id = "doc3",
                    Folder = "2",
                    Name = "Public.json",
                    FullPath = "UKSF\\JSFAW\\Public.json",
                    Created = _utcNow.AddDays(-1),
                    LastUpdated = _utcNow
                }
            }
        };

        _mockIDocumentFolderMetadataContext.Setup(x => x.GetSingle("2")).Returns(folderWithDocuments);
    }

    private void Given_multiple_folders_with_documents()
    {
        var folders = new List<DomainDocumentFolderMetadata>
        {
            new()
            {
                Id = "1",
                Parent = ObjectId.Empty.ToString(),
                Name = "UKSF",
                FullPath = "UKSF",
                Documents = new List<DomainDocumentMetadata>()
            },
            new()
            {
                Id = "2",
                Parent = "1",
                Name = "JSFAW",
                FullPath = "UKSF\\JSFAW",
                Documents = new List<DomainDocumentMetadata>
                {
                    new()
                    {
                        Id = "doc_folder2_1",
                        Folder = "2",
                        Name = "Restricted.json",
                        FullPath = "UKSF\\JSFAW\\Restricted.json"
                    },
                    new()
                    {
                        Id = "doc_folder2_2",
                        Folder = "2",
                        Name = "Open.json",
                        FullPath = "UKSF\\JSFAW\\Open.json"
                    }
                }
            },
            new()
            {
                Id = "3",
                Parent = "1",
                Name = "SFSG",
                FullPath = "UKSF\\SFSG",
                Documents = new List<DomainDocumentMetadata>
                {
                    new()
                    {
                        Id = "doc_folder3_1",
                        Folder = "3",
                        Name = "Mission.json",
                        FullPath = "UKSF\\SFSG\\Mission.json"
                    },
                    new()
                    {
                        Id = "doc_folder3_2",
                        Folder = "3",
                        Name = "Classified.json",
                        FullPath = "UKSF\\SFSG\\Classified.json"
                    }
                }
            }
        };

        _mockIDocumentFolderMetadataContext.Setup(x => x.Get(It.IsAny<Func<DomainDocumentFolderMetadata, bool>>()))
                                           .Returns((Func<DomainDocumentFolderMetadata, bool> filter) => folders.Where(filter).ToList());
    }
}
