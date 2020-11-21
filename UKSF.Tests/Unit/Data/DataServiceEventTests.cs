using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class DataServiceEventTests {
        private readonly string _id1;
        private readonly string _id2;
        private readonly string _id3;
        private readonly TestDataModel _item1;
        private readonly Mock<Api.Base.Context.IMongoCollection<TestDataModel>> _mockDataCollection;
        private readonly Mock<IDataEventBus<TestDataModel>> _mockDataEventBus;
        private readonly TestContext _testContext;

        public DataServiceEventTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            _mockDataEventBus = new Mock<IDataEventBus<TestDataModel>>();
            _mockDataCollection = new Mock<Api.Base.Context.IMongoCollection<TestDataModel>>();
            _id1 = ObjectId.GenerateNewId().ToString();
            _id2 = ObjectId.GenerateNewId().ToString();
            _id3 = ObjectId.GenerateNewId().ToString();
            _item1 = new TestDataModel { Id = _id1, Name = "1" };
            TestDataModel item2 = new() { Id = _id2, Name = "1" };
            TestDataModel item3 = new() { Id = _id3, Name = "3" };
            List<TestDataModel> mockCollection = new() { _item1, item2, item3 };

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<TestDataModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
            _mockDataCollection.Setup(x => x.Get()).Returns(() => mockCollection);
            _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(predicate => mockCollection.Where(predicate));
            _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(predicate => mockCollection.FirstOrDefault(predicate));
            _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(id => mockCollection.FirstOrDefault(x => x.Id == id));

            _testContext = new TestContext(mockDataCollectionFactory.Object, _mockDataEventBus.Object, "test");
        }

        [Fact]
        public async Task Should_create_correct_add_event_for_add() {
            DataEventModel<TestDataModel> subject = null;

            _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<TestDataModel>())).Returns(Task.CompletedTask);
            _mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await _testContext.Add(_item1);

            _mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { Id = _id1, Type = DataEventType.ADD, Data = _item1 });
        }

        [Fact]
        public async Task Should_create_correct_delete_event_for_delete() {
            DataEventModel<TestDataModel> subject = null;

            _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await _testContext.Delete(new TestDataModel { Id = _id1 });

            _mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { Id = _id1, Type = DataEventType.DELETE, Data = null });
        }

        [Fact]
        public async Task Should_create_correct_delete_event_for_delete_by_id() {
            DataEventModel<TestDataModel> subject = null;

            _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await _testContext.Delete(_id1);

            _mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { Id = _id1, Type = DataEventType.DELETE, Data = null });
        }

        [Fact]
        public async Task Should_create_correct_delete_events_for_delete_many() {
            List<DataEventModel<TestDataModel>> subjects = new();

            _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>())).Returns(Task.CompletedTask);
            _mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subjects.Add(dataEventModel));

            await _testContext.DeleteMany(x => x.Name == "1");

            _mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Exactly(2));
            subjects.Should()
                    .BeEquivalentTo(
                        new DataEventModel<TestDataModel> { Id = _id1, Type = DataEventType.DELETE, Data = null },
                        new DataEventModel<TestDataModel> { Id = _id2, Type = DataEventType.DELETE, Data = null }
                    );
        }

        [Fact]
        public async Task Should_create_correct_update_event_for_replace() {
            DataEventModel<TestDataModel> subject = null;

            _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<TestDataModel>())).Returns(Task.CompletedTask);
            _mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await _testContext.Replace(_item1);

            _mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { Id = _id1, Type = DataEventType.UPDATE, Data = null });
        }

        [Fact]
        public async Task Should_create_correct_update_events_for_update_many() {
            List<DataEventModel<TestDataModel>> subjects = new();

            _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);
            _mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subjects.Add(dataEventModel));

            await _testContext.UpdateMany(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

            _mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Exactly(2));
            subjects.Should()
                    .BeEquivalentTo(
                        new DataEventModel<TestDataModel> { Id = _id1, Type = DataEventType.UPDATE, Data = null },
                        new DataEventModel<TestDataModel> { Id = _id2, Type = DataEventType.UPDATE, Data = null }
                    );
        }

        [Fact]
        public async Task Should_create_correct_update_events_for_updates() {
            List<DataEventModel<TestDataModel>> subjects = new();

            _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);
            _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<TestDataModel>>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);
            _mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subjects.Add(dataEventModel));

            await _testContext.Update(_id1, "Name", "1");
            await _testContext.Update(_id2, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));
            await _testContext.Update(x => x.Id == _id3, Builders<TestDataModel>.Update.Set(x => x.Name, "3"));

            _mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Exactly(3));
            subjects.Should()
                    .BeEquivalentTo(
                        new DataEventModel<TestDataModel> { Id = _id1, Type = DataEventType.UPDATE, Data = null },
                        new DataEventModel<TestDataModel> { Id = _id2, Type = DataEventType.UPDATE, Data = null },
                        new DataEventModel<TestDataModel> { Id = _id3, Type = DataEventType.UPDATE, Data = null }
                    );
        }
    }
}
