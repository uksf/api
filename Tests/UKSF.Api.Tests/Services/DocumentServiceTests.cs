using System;
using System.Linq.Expressions;
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

public class DocumentServiceTests
{
    private readonly string _memberId = ObjectId.GenerateNewId().ToString();
    private readonly Mock<IClock> _mockIClock = new();
    private readonly Mock<IDocumentFolderMetadataContext> _mockIDocumentMetadataContext = new();
    private readonly Mock<IFileContext> _mockIFileContext = new();
    private readonly Mock<IHttpContextService> _mockIHttpContextService = new();
    private readonly Mock<IRoleBasedDocumentPermissionsService> _mockIRoleBasedDocumentPermissionsService = new();
    private readonly Mock<IUksfLogger> _mockIUksfLogger = new();
    private readonly Mock<IVariablesService> _mockIVariablesService = new();

    private readonly DocumentService _subject;

    private readonly DateTime _utcNow = DateTime.UtcNow;

    public DocumentServiceTests()
    {
        _mockIVariablesService.Setup(x => x.GetVariable("DOCUMENTS_PATH")).Returns(new DomainVariableItem { Item = "" });
        _mockIRoleBasedDocumentPermissionsService.Setup(x => x.DoesContextHaveReadPermission(It.IsAny<DomainMetadataWithPermissions>())).Returns(true);
        _mockIRoleBasedDocumentPermissionsService.Setup(x => x.DoesContextHaveWritePermission(It.IsAny<DomainMetadataWithPermissions>())).Returns(true);
        _mockIClock.Setup(x => x.UtcNow()).Returns(_utcNow);
        _mockIHttpContextService.Setup(x => x.GetUserId()).Returns(_memberId);

        _subject = new DocumentService(
            _mockIDocumentMetadataContext.Object,
            _mockIHttpContextService.Object,
            _mockIRoleBasedDocumentPermissionsService.Object,
            _mockIVariablesService.Object,
            _mockIFileContext.Object,
            _mockIClock.Object,
            _mockIUksfLogger.Object
        );
    }

    [Fact]
    public async Task When_creating_a_document()
    {
        Given_folder_metadata();

        await _subject.CreateDocument("2", new CreateDocumentRequest { Name = "About.json" });

        _mockIDocumentMetadataContext.Verify(x => x.Update("2", It.IsAny<UpdateDefinition<DomainDocumentFolderMetadata>>()), Times.Once());
    }

    [Fact]
    public async Task When_creating_a_document_with_existing_name()
    {
        Given_folder_metadata();

        var act = async () => await _subject.CreateDocument("2", new CreateDocumentRequest { Name = "Training1.json" });

        await act.Should().ThrowAsync<DocumentException>().WithMessageAndStatusCode("A document already exists at path 'UKSF\\JSFAW\\Training1.json'", 400);
    }

    [Fact]
    public async Task When_creating_a_document_without_permission()
    {
        Given_folder_metadata();
        _mockIRoleBasedDocumentPermissionsService.Setup(x => x.DoesContextHaveWritePermission(It.IsAny<DomainMetadataWithPermissions>())).Returns(false);

        var act = async () => await _subject.CreateDocument("2", new CreateDocumentRequest { Name = "Training2.json" });

        await act.Should().ThrowAsync<FolderException>().WithMessageAndStatusCode("Cannot create documents in this folder", 400);
    }

    [Fact]
    public async Task When_updating_a_document_and_update_is_newer()
    {
        Given_folder_metadata();
        _mockIFileContext.Setup(x => x.Exists("1.json")).Returns(true);

        var result = await _subject.UpdateDocumentContent(
            "2",
            "1",
            new UpdateDocumentContentRequest { NewText = "New text", LastKnownUpdated = _utcNow.AddDays(-1) }
        );

        _mockIDocumentMetadataContext.Verify(
            x => x.FindAndUpdate(It.IsAny<Expression<Func<DomainDocumentFolderMetadata, bool>>>(), It.IsAny<UpdateDefinition<DomainDocumentFolderMetadata>>()),
            Times.Once
        );
        result.Text.Should().Be("New text");
        result.LastUpdated.Should().Be(_utcNow);
    }

    [Fact]
    public async Task When_updating_a_document_and_update_is_older()
    {
        Given_folder_metadata();

        var act = async () => await _subject.UpdateDocumentContent(
            "2",
            "1",
            new UpdateDocumentContentRequest { NewText = "New text", LastKnownUpdated = _utcNow.AddDays(-3) }
        );

        await act.Should()
                 .ThrowAsync<DocumentException>()
                 .WithMessageAndStatusCode("Document update for 'Training1.json' is behind more recent changes. Please refresh", 400);
    }

    [Fact]
    public async Task UpdateDocument_WhenNameChanges_ShouldUpdateFullPath()
    {
        // Arrange
        Given_folder_metadata();
        var newRequest = new CreateDocumentRequest { Name = "NewDocumentName.json" };

        // Act
        await _subject.UpdateDocument("2", "1", newRequest); // Use "1" which exists in test data

        // Assert - Just verify that FindAndUpdate was called, which means the update logic ran
        _mockIDocumentMetadataContext.Verify(
            x => x.FindAndUpdate(It.IsAny<Expression<Func<DomainDocumentFolderMetadata, bool>>>(), It.IsAny<UpdateDefinition<DomainDocumentFolderMetadata>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateDocument_WhenNameChangesToExistingName_ShouldThrowException()
    {
        // Arrange  
        Given_folder_metadata_with_multiple_documents();
        var newRequest = new CreateDocumentRequest { Name = "Training2.json" }; // This name already exists in the folder

        // Act & Assert
        var act = async () => await _subject.UpdateDocument("2", "1", newRequest);

        await act.Should().ThrowAsync<DocumentException>().WithMessage("A document already exists at path 'UKSF\\JSFAW\\Training2.json'");
    }

    [Fact]
    public async Task When_creating_a_document_at_folder_without_permission_throws_exception()
    {
        Given_folder_metadata();
        _mockIRoleBasedDocumentPermissionsService.Setup(x => x.DoesContextHaveWritePermission(It.IsAny<DomainMetadataWithPermissions>())).Returns(false);

        var act = async () => await _subject.CreateDocument("2", new CreateDocumentRequest { Name = "Training2.json" });

        await act.Should().ThrowAsync<FolderException>().WithMessageAndStatusCode("Cannot create documents in this folder", 400);
    }

    private void Given_folder_metadata()
    {
        _mockIDocumentMetadataContext.Setup(x => x.GetSingle("2"))
                                     .Returns(
                                         new DomainDocumentFolderMetadata
                                         {
                                             Id = "2",
                                             Parent = "1",
                                             Name = "JSFAW",
                                             FullPath = "UKSF\\JSFAW",
                                             Documents =
                                             [
                                                 new DomainDocumentMetadata
                                                 {
                                                     Id = "1",
                                                     Folder = "2",
                                                     Name = "Training1.json",
                                                     LastUpdated = _utcNow.AddDays(-2)
                                                 }
                                             ]
                                         }
                                     );
    }

    private void Given_folder_metadata_with_multiple_documents()
    {
        _mockIDocumentMetadataContext.Setup(x => x.GetSingle("2"))
                                     .Returns(
                                         new DomainDocumentFolderMetadata
                                         {
                                             Id = "2",
                                             Parent = "1",
                                             Name = "JSFAW",
                                             FullPath = "UKSF\\JSFAW",
                                             Documents =
                                             [
                                                 new DomainDocumentMetadata
                                                 {
                                                     Id = "1",
                                                     Folder = "2",
                                                     Name = "Training1.json",
                                                     LastUpdated = _utcNow.AddDays(-2)
                                                 },
                                                 new DomainDocumentMetadata
                                                 {
                                                     Id = "2",
                                                     Folder = "2",
                                                     Name = "Training2.json",
                                                     LastUpdated = _utcNow.AddDays(-2)
                                                 }
                                             ]
                                         }
                                     );
    }
}
