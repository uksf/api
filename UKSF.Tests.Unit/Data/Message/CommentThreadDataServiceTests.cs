﻿using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Data;
using UKSF.Api.Data.Message;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using Xunit;

namespace UKSF.Tests.Unit.Data.Message {
    public class CommentThreadDataServiceTests {
        private readonly CommentThreadDataService commentThreadDataService;
        private readonly Mock<IDataCollection> mockDataCollection;
        private List<CommentThread> mockCollection;

        public CommentThreadDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<ICommentThreadDataService>> mockDataEventBus = new Mock<IDataEventBus<ICommentThreadDataService>>();
            mockDataCollection = new Mock<IDataCollection>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get<CommentThread>()).Returns(() => mockCollection);

            commentThreadDataService = new CommentThreadDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public async Task ShouldCreateCorrectUdpateDefinitionForAdd() {
            CommentThread commentThread = new CommentThread();
            mockCollection = new List<CommentThread> { commentThread };

            Comment comment = new Comment { author = ObjectId.GenerateNewId().ToString(), content = "Hello there" };
            BsonValue expected = TestUtilities.Render(Builders<CommentThread>.Update.Push(x => x.comments, comment));
            UpdateDefinition<CommentThread> subject = null;

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<CommentThread>>()))
                              .Returns(Task.CompletedTask)
                              .Callback<string, UpdateDefinition<CommentThread>>((_, update) => subject = update);

            await commentThreadDataService.Update(commentThread.id, comment, DataEventType.ADD);

            TestUtilities.Render(subject).Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task ShouldCreateCorrectUdpateDefinitionForDelete() {
            CommentThread commentThread = new CommentThread();
            mockCollection = new List<CommentThread> { commentThread };

            Comment comment = new Comment { author = ObjectId.GenerateNewId().ToString(), content = "Hello there" };
            BsonValue expected = TestUtilities.Render(Builders<CommentThread>.Update.Pull(x => x.comments, comment));
            UpdateDefinition<CommentThread> subject = null;

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<CommentThread>>()))
                              .Returns(Task.CompletedTask)
                              .Callback<string, UpdateDefinition<CommentThread>>((_, update) => subject = update);

            await commentThreadDataService.Update(commentThread.id, comment, DataEventType.DELETE);

            TestUtilities.Render(subject).Should().BeEquivalentTo(expected);
        }
    }
}