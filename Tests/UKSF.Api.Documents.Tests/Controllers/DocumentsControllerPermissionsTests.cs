using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Documents.Commands;
using UKSF.Api.Documents.Context;
using UKSF.Api.Documents.Controllers;
using UKSF.Api.Documents.Exceptions;
using UKSF.Api.Documents.Mappers;
using UKSF.Api.Documents.Models;
using UKSF.Api.Documents.Queries;
using UKSF.Api.Shared.Exceptions;
using Xunit;

namespace UKSF.Api.Documents.Tests.Controllers {
    public class DocumentsControllerPermissionsTests {
        private readonly Mock<IDocumentsMetadataContext> _mockDocumentsMetadataContext;
        private readonly Mock<IUserPermissionsForDocumentQuery> _mockUserHasPermissionsForDocumentQuery;
        private readonly Mock<IVerifyDocumentPermissionsCommand> _mockVerifyDocumentPermissionsCommand;
        private readonly DocumentsController _subject;
        private readonly string _documentId = ObjectId.GenerateNewId().ToString();

        public DocumentsControllerPermissionsTests() {
            _mockDocumentsMetadataContext = new();
            _mockUserHasPermissionsForDocumentQuery = new();
            _mockVerifyDocumentPermissionsCommand = new();

            _subject = new(_mockDocumentsMetadataContext.Object, _mockUserHasPermissionsForDocumentQuery.Object, new DocumentMetadataMapper(), _mockVerifyDocumentPermissionsCommand.Object);
        }

        [Fact]
        public async Task When_setting_document_permissions_that_doesnt_exist() {
            _mockDocumentsMetadataContext.Setup(x => x.GetSingle(_documentId)).Returns((ContextDocumentMetadata) null);

            Func<Task> act = async () => await _subject.SetDocumentPermissions(_documentId, new());

            ExceptionAssertions<UksfNotFoundException> result = await act.Should().ThrowAsync<UksfNotFoundException>().WithMessage($"Document with id {_documentId} not found");
            result.And.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task When_setting_document_permissions_without_permission() {
            ContextDocumentMetadata contextDocument = new() { Id = _documentId };

            _mockDocumentsMetadataContext.Setup(x => x.GetSingle(_documentId)).Returns(contextDocument);
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.IsAny<UserPermissionsForDocumentQueryArgs>())).Returns(new UserPermissionsForDocumentResult(false, false));

            Func<Task> act = async () => await _subject.SetDocumentPermissions(_documentId, new());

            ExceptionAssertions<UksfUnauthorizedException> result = await act.Should().ThrowAsync<UksfUnauthorizedException>().WithMessage("Unauthorized");
            result.And.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task When_setting_document_permissions_with_invalid_permissions_object() {
            ContextDocumentMetadata contextDocument = new() { Id = _documentId };

            _mockDocumentsMetadataContext.Setup(x => x.GetSingle(_documentId)).Returns(contextDocument);
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.IsAny<UserPermissionsForDocumentQueryArgs>())).Returns(new UserPermissionsForDocumentResult(true, true));
            _mockVerifyDocumentPermissionsCommand.Setup(x => x.Execute(It.IsAny<VerifyDocumentPermissionsCommandArgs>())).Throws<UksfInvalidDocumentPermissionsException>();

            DocumentPermissions permissions = new();
            Func<Task> act = async () => await _subject.SetDocumentPermissions(_documentId, permissions);

            ExceptionAssertions<UksfInvalidDocumentPermissionsException> result = await act.Should().ThrowAsync<UksfInvalidDocumentPermissionsException>().WithMessage("Invalid document permissions object");
            result.And.StatusCode.Should().Be(400);
        }

        [Fact]
        public void When_setting_document_permissions() {
            ContextDocumentMetadata contextDocument = new() { Id = _documentId };

            _mockDocumentsMetadataContext.Setup(x => x.GetSingle(_documentId)).Returns(contextDocument);
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.IsAny<UserPermissionsForDocumentQueryArgs>())).Returns(new UserPermissionsForDocumentResult(true, true));

            DocumentPermissions permissions = new();
            _subject.SetDocumentPermissions(_documentId, permissions);

            _mockVerifyDocumentPermissionsCommand.Verify(x => x.Execute(It.Is<VerifyDocumentPermissionsCommandArgs>(m => m.Document == contextDocument && m.Permissions == permissions)), Times.Once);
            _mockDocumentsMetadataContext.Verify(x => x.Update(_documentId, It.IsAny<Expression<Func<ContextDocumentMetadata,object>>>(), permissions));
        }
    }
}
