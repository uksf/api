using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Documents.Context;
using UKSF.Api.Documents.Controllers;
using UKSF.Api.Documents.Mappers;
using UKSF.Api.Documents.Models;
using UKSF.Api.Documents.Queries;
using UKSF.Api.Shared.Exceptions;
using Xunit;

namespace UKSF.Api.Documents.Tests.Controllers {
    public class DocumentsControllerTests {
        private readonly Mock<IDocumentsMetadataContext> _mockDocumentsMetadataContext;
        private readonly Mock<IUserPermissionsForDocumentQuery> _mockUserHasPermissionsForDocumentQuery;
        private readonly DocumentsController _subject;

        public DocumentsControllerTests() {
            _mockDocumentsMetadataContext = new();
            _mockUserHasPermissionsForDocumentQuery = new();

            _subject = new(_mockDocumentsMetadataContext.Object, _mockUserHasPermissionsForDocumentQuery.Object, new DocumentMetadataMapper());
        }

        [Fact]
        public void When_getting_all_documents() {
            _mockDocumentsMetadataContext.Setup(x => x.Get()).Returns(new List<ContextDocumentMetadata> { new(), new() });
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.IsAny<UserPermissionsForDocumentQueryArgs>())).Returns(new UserPermissionsForDocumentResult(true, true));

            IEnumerable<DocumentMetadata> results = _subject.GetAllDocuments();

            IEnumerable<DocumentMetadata> resultsList = results.ToList();
            resultsList.Count().Should().Be(2);
            resultsList.All(x => x.CanView & x.CanEdit).Should().BeTrue();
        }

        [Fact]
        public void When_getting_all_documents_with_permission_for_some() {
            Given_permission_to_view_some_documents();

            IEnumerable<DocumentMetadata> results = _subject.GetAllDocuments().ToList();

            results.Count().Should().Be(1);
            results.Single().CanView.Should().BeTrue();
            results.Single().CanEdit.Should().BeTrue();
        }

        [Fact]
        public void When_getting_single_document() {
            string documentId = ObjectId.GenerateNewId().ToString();
            ContextDocumentMetadata contextDocument = new() { Id = documentId };

            _mockDocumentsMetadataContext.Setup(x => x.GetSingle(documentId)).Returns(contextDocument);
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.IsAny<UserPermissionsForDocumentQueryArgs>())).Returns(new UserPermissionsForDocumentResult(true, true));

            DocumentMetadata result = _subject.GetDocument(documentId);

            result.Id.Should().Be(documentId);
            result.CanView.Should().BeTrue();
        }

        [Fact]
        public void When_getting_single_document_that_doesnt_exist() {
            string documentId = ObjectId.GenerateNewId().ToString();
            _mockDocumentsMetadataContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns((ContextDocumentMetadata) null);

            Action act = () => _subject.GetDocument(documentId);

            act.Should().Throw<UksfNotFoundException>().WithMessage($"Document with id {documentId} not found").And.StatusCode.Should().Be(404);
        }

        [Fact]
        public void When_getting_single_document_without_permission() {
            string documentId = ObjectId.GenerateNewId().ToString();
            ContextDocumentMetadata contextDocument = new() { Id = documentId };

            _mockDocumentsMetadataContext.Setup(x => x.GetSingle(documentId)).Returns(contextDocument);
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.IsAny<UserPermissionsForDocumentQueryArgs>())).Returns(new UserPermissionsForDocumentResult(false, false));

            Action act = () => _subject.GetDocument(documentId);

            act.Should().Throw<UksfUnauthorizedException>().WithMessage("Unauthorized").And.StatusCode.Should().Be(401);
        }

        private void Given_permission_to_view_some_documents() {
            string documentId = ObjectId.GenerateNewId().ToString();
            _mockDocumentsMetadataContext.Setup(x => x.Get()).Returns(new List<ContextDocumentMetadata> { new() { Id = documentId }, new() });
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.Is<UserPermissionsForDocumentQueryArgs>(m => m.ContextDocument.Id == documentId)))
                                                   .Returns(new UserPermissionsForDocumentResult(true, true));
            _mockUserHasPermissionsForDocumentQuery.Setup(x => x.Execute(It.Is<UserPermissionsForDocumentQueryArgs>(m => m.ContextDocument.Id != documentId)))
                                                   .Returns(new UserPermissionsForDocumentResult(false, false));
        }
    }
}
