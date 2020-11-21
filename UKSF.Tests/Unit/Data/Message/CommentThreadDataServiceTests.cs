using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data.Message {
    public class CommentThreadDataServiceTests {
        private readonly CommentThreadContext _commentThreadContext;
        private readonly Mock<Api.Base.Context.IMongoCollection<CommentThread>> _mockDataCollection;
        private List<CommentThread> _mockCollection;

        public CommentThreadDataServiceTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IDataEventBus<CommentThread>> mockDataEventBus = new();
            _mockDataCollection = new Mock<Api.Base.Context.IMongoCollection<CommentThread>>();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<CommentThread>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
            _mockDataCollection.Setup(x => x.Get()).Returns(() => _mockCollection);

            _commentThreadContext = new CommentThreadContext(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public async Task ShouldCreateCorrectUdpateDefinitionForAdd() {
            CommentThread commentThread = new();
            _mockCollection = new List<CommentThread> { commentThread };

            Comment comment = new() { Author = ObjectId.GenerateNewId().ToString(), Content = "Hello there" };
            BsonValue expected = TestUtilities.RenderUpdate(Builders<CommentThread>.Update.Push(x => x.Comments, comment));
            UpdateDefinition<CommentThread> subject = null;

            _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<CommentThread>>()))
                               .Returns(Task.CompletedTask)
                               .Callback<string, UpdateDefinition<CommentThread>>((_, update) => subject = update);

            await _commentThreadContext.Update(commentThread.Id, comment, DataEventType.ADD);

            TestUtilities.RenderUpdate(subject).Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task ShouldCreateCorrectUdpateDefinitionForDelete() {
            CommentThread commentThread = new();
            _mockCollection = new List<CommentThread> { commentThread };

            Comment comment = new() { Author = ObjectId.GenerateNewId().ToString(), Content = "Hello there" };
            BsonValue expected = TestUtilities.RenderUpdate(Builders<CommentThread>.Update.Pull(x => x.Comments, comment));
            UpdateDefinition<CommentThread> subject = null;

            _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<CommentThread>>()))
                               .Returns(Task.CompletedTask)
                               .Callback<string, UpdateDefinition<CommentThread>>((_, update) => subject = update);

            await _commentThreadContext.Update(commentThread.Id, comment, DataEventType.DELETE);

            TestUtilities.RenderUpdate(subject).Should().BeEquivalentTo(expected);
        }
    }
}
