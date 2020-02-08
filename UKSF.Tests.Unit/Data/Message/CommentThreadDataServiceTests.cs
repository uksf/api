using System.Collections.Generic;
using Moq;
using UKSF.Api.Data.Message;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Message;
using Xunit;

namespace UKSF.Tests.Unit.Data.Message {
    public class CommentThreadDataServiceTests {
        private readonly CommentThreadDataService commentThreadDataService;
        private readonly Mock<IDataCollection> mockDataCollection;
        private List<CommentThread> mockCollection;

        public CommentThreadDataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            Mock<IDataEventBus<ICommentThreadDataService>> mockdataEventBus = new Mock<IDataEventBus<ICommentThreadDataService>>();
            commentThreadDataService = new CommentThreadDataService(mockDataCollection.Object, mockdataEventBus.Object);

            mockCollection = new List<CommentThread>();

            mockDataCollection.Setup(x => x.Get<CommentThread>()).Returns(() => mockCollection);
        }

        [Fact]
        public void ShouldReturnCommentThreadId() {

        }
    }
}
