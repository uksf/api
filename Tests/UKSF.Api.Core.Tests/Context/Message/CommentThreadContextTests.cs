using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Message;

public class CommentThreadContextTests
{
    private readonly CommentThreadContext _commentThreadContext;
    private readonly Mock<Core.Context.Base.IMongoCollection<DomainCommentThread>> _mockDataCollection;
    private List<DomainCommentThread> _mockCollection;

    public CommentThreadContextTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new Mock<Core.Context.Base.IMongoCollection<DomainCommentThread>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainCommentThread>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _mockDataCollection.Setup(x => x.Get()).Returns(() => _mockCollection);

        _commentThreadContext = new CommentThreadContext(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public async Task ShouldCreateCorrectUdpateDefinitionForAdd()
    {
        DomainCommentThread commentThread = new();
        _mockCollection = [commentThread];

        DomainComment comment = new() { Author = ObjectId.GenerateNewId().ToString(), Content = "Hello there" };
        var expected = Builders<DomainCommentThread>.Update.Push(x => x.Comments, comment).RenderUpdate();
        UpdateDefinition<DomainCommentThread> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainCommentThread>>()))
                           .Returns(Task.CompletedTask)
                           .Callback<string, UpdateDefinition<DomainCommentThread>>((_, update) => subject = update);

        await _commentThreadContext.AddCommentToThread(commentThread.Id, comment);

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ShouldCreateCorrectUdpateDefinitionForDelete()
    {
        DomainCommentThread commentThread = new();
        _mockCollection = [commentThread];

        DomainComment comment = new() { Author = ObjectId.GenerateNewId().ToString(), Content = "Hello there" };
        var expected = Builders<DomainCommentThread>.Update.Pull(x => x.Comments, comment).RenderUpdate();
        UpdateDefinition<DomainCommentThread> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainCommentThread>>()))
                           .Returns(Task.CompletedTask)
                           .Callback<string, UpdateDefinition<DomainCommentThread>>((_, update) => subject = update);

        await _commentThreadContext.RemoveCommentFromThread(commentThread.Id, comment);

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }
}
