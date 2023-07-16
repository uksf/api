using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data.Message;

public class CommentThreadDataServiceTests
{
    private readonly CommentThreadContext _commentThreadContext;
    private readonly Mock<Api.Core.Context.Base.IMongoCollection<CommentThread>> _mockDataCollection;
    private List<CommentThread> _mockCollection;

    public CommentThreadDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<CommentThread>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _mockDataCollection.Setup(x => x.Get()).Returns(() => _mockCollection);

        _commentThreadContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public async Task ShouldCreateCorrectUdpateDefinitionForAdd()
    {
        CommentThread commentThread = new();
        _mockCollection = new() { commentThread };

        Comment comment = new() { Author = ObjectId.GenerateNewId().ToString(), Content = "Hello there" };
        var expected = Builders<CommentThread>.Update.Push(x => x.Comments, comment).RenderUpdate();
        UpdateDefinition<CommentThread> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<CommentThread>>()))
                           .Returns(Task.CompletedTask)
                           .Callback<string, UpdateDefinition<CommentThread>>((_, update) => subject = update);

        await _commentThreadContext.AddCommentToThread(commentThread.Id, comment);

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ShouldCreateCorrectUdpateDefinitionForDelete()
    {
        CommentThread commentThread = new();
        _mockCollection = new() { commentThread };

        Comment comment = new() { Author = ObjectId.GenerateNewId().ToString(), Content = "Hello there" };
        var expected = Builders<CommentThread>.Update.Pull(x => x.Comments, comment).RenderUpdate();
        UpdateDefinition<CommentThread> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<CommentThread>>()))
                           .Returns(Task.CompletedTask)
                           .Callback<string, UpdateDefinition<CommentThread>>((_, update) => subject = update);

        await _commentThreadContext.RemoveCommentFromThread(commentThread.Id, comment);

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }
}
