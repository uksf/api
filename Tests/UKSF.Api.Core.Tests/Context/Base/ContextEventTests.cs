using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Base;

public class ContextEventTests
{
    private readonly string _id1;
    private readonly string _id2;
    private readonly string _id3;
    private readonly DomainTestModel _item1;
    private readonly Mock<Core.Context.Base.IMongoCollection<DomainTestModel>> _mockDataCollection;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly TestContext _testContext;

    public ContextEventTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        _mockEventBus = new Mock<IEventBus>();
        _mockDataCollection = new Mock<Core.Context.Base.IMongoCollection<DomainTestModel>>();
        _id1 = ObjectId.GenerateNewId().ToString();
        _id2 = ObjectId.GenerateNewId().ToString();
        _id3 = ObjectId.GenerateNewId().ToString();
        _item1 = new DomainTestModel { Id = _id1, Name = "1" };
        DomainTestModel item2 = new() { Id = _id2, Name = "1" };
        DomainTestModel item3 = new() { Id = _id3, Name = "3" };
        List<DomainTestModel> mockCollection = [_item1, item2, item3];

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainTestModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<DomainTestModel, bool>>()))
                           .Returns<Func<DomainTestModel, bool>>(predicate => mockCollection.Where(predicate));
        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<DomainTestModel, bool>>()))
                           .Returns<Func<DomainTestModel, bool>>(predicate => mockCollection.FirstOrDefault(predicate));

        _testContext = new TestContext(mockDataCollectionFactory.Object, _mockEventBus.Object, "test");
    }

    [Fact]
    public async Task Should_create_correct_add_event_for_add()
    {
        EventModel subject = null;

        _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(dataEventModel => subject = dataEventModel);

        await _testContext.Add(_item1);

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventModel>()), Times.Once);
        subject.Should().BeEquivalentTo(new EventModel(EventType.Add, new ContextEventData<DomainTestModel>(string.Empty, _item1), "test.Add"));
    }

    [Fact]
    public async Task Should_create_correct_delete_event_for_delete()
    {
        EventModel subject = null;

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(dataEventModel => subject = dataEventModel);

        await _testContext.Delete(new DomainTestModel { Id = _id1 });

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventModel>()), Times.Once);
        subject.Should().BeEquivalentTo(new EventModel(EventType.Delete, new ContextEventData<DomainTestModel>(_id1, null), "test.Delete"));
    }

    [Fact]
    public async Task Should_create_correct_delete_event_for_delete_by_id()
    {
        EventModel subject = null;

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(dataEventModel => subject = dataEventModel);

        await _testContext.Delete(_id1);

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventModel>()), Times.Once);
        subject.Should().BeEquivalentTo(new EventModel(EventType.Delete, new ContextEventData<DomainTestModel>(_id1, null), "test.Delete"));
    }

    [Fact]
    public async Task Should_create_correct_delete_events_for_delete_many()
    {
        List<EventModel> subjects = new();

        _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>())).Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(dataEventModel => subjects.Add(dataEventModel));

        await _testContext.DeleteMany(x => x.Name == "1");

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventModel>()), Times.Exactly(2));
        subjects.Should()
                .BeEquivalentTo(
                    new List<EventModel>
                    {
                        new(EventType.Delete, new ContextEventData<DomainTestModel>(_id1, null), "test.Delete"),
                        new(EventType.Delete, new ContextEventData<DomainTestModel>(_id2, null), "test.Delete")
                    }
                );
    }

    [Fact]
    public async Task Should_create_correct_update_event_for_replace()
    {
        EventModel subject = null;

        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(dataEventModel => subject = dataEventModel);

        await _testContext.Replace(_item1);

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventModel>()), Times.Once);
        subject.Should().BeEquivalentTo(new EventModel(EventType.Update, new ContextEventData<DomainTestModel>(_id1, null), "test.Update"));
    }

    [Fact]
    public async Task Should_create_correct_update_events_for_update_many()
    {
        List<EventModel> subjects = new();

        _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(dataEventModel => subjects.Add(dataEventModel));

        await _testContext.UpdateMany(x => x.Name == "1", Builders<DomainTestModel>.Update.Set(x => x.Name, "2"));

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventModel>()), Times.Exactly(2));
        subjects.Should()
                .BeEquivalentTo(
                    new List<EventModel>
                    {
                        new(EventType.Update, new ContextEventData<DomainTestModel>(_id1, null), "test.Update"),
                        new(EventType.Update, new ContextEventData<DomainTestModel>(_id2, null), "test.Update")
                    }
                );
    }

    [Fact]
    public async Task Should_create_correct_update_events_for_updates()
    {
        List<EventModel> subjects = new();

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<DomainTestModel>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(dataEventModel => subjects.Add(dataEventModel));

        await _testContext.Update(_id1, x => x.Name, "1");
        await _testContext.Update(_id2, Builders<DomainTestModel>.Update.Set(x => x.Name, "2"));
        await _testContext.Update(x => x.Id == _id3, Builders<DomainTestModel>.Update.Set(x => x.Name, "3"));

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventModel>()), Times.Exactly(3));
        subjects.Should()
                .BeEquivalentTo(
                    new List<EventModel>
                    {
                        new(EventType.Update, new ContextEventData<DomainTestModel>(_id1, null), "test.Update"),
                        new(EventType.Update, new ContextEventData<DomainTestModel>(_id2, null), "test.Update"),
                        new(EventType.Update, new ContextEventData<DomainTestModel>(_id3, null), "test.Update")
                    }
                );
    }
}
