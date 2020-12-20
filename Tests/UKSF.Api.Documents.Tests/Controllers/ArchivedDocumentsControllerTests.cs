using MongoDB.Bson;
using Moq;
using UKSF.Api.Documents.Context;
using UKSF.Api.Documents.Controllers;
using Xunit;

namespace UKSF.Api.Documents.Tests.Controllers {
    public class ArchivedDocumentsControllerTests {
        private readonly Mock<IArchivedDocumentsMetadataContext> _mockArchivedDocumentsMetadataContext;
        private readonly ArchivedDocumentsController _subject;

        public ArchivedDocumentsControllerTests() {
            _mockArchivedDocumentsMetadataContext = new Mock<IArchivedDocumentsMetadataContext>();

            _subject = new ArchivedDocumentsController(_mockArchivedDocumentsMetadataContext.Object);
        }

        [Fact]
        public void When_getting_all_documents() {
            _subject.GetAllArchivedDocuments();

            _mockArchivedDocumentsMetadataContext.Verify(x => x.Get(), Times.Once);
        }

        [Fact]
        public void When_getting_single_document() {
            string documentId = ObjectId.GenerateNewId().ToString();

            _subject.GetArchivedDocument(documentId);

            _mockArchivedDocumentsMetadataContext.Verify(x => x.GetSingle(documentId), Times.Once);
        }
    }
}
