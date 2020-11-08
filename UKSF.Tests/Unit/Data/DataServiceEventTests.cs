using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class DataServiceEventTests {
        private readonly string id1;
        private readonly string id2;
        private readonly string id3;
        private readonly TestDataModel item1;
        private readonly Mock<IDataCollection<TestDataModel>> mockDataCollection;
        private readonly Mock<IDataEventBus<TestDataModel>> mockDataEventBus;
        private readonly TestDataService testDataService;

        public DataServiceEventTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockDataEventBus = new Mock<IDataEventBus<TestDataModel>>();
            mockDataCollection = new Mock<IDataCollection<TestDataModel>>();
            id1 = ObjectId.GenerateNewId().ToString();
            id2 = ObjectId.GenerateNewId().ToString();
            id3 = ObjectId.GenerateNewId().ToString();
            item1 = new TestDataModel { id = id1, Name = "1" };
            TestDataModel item2 = new TestDataModel { id = id2, Name = "1" };
            TestDataModel item3 = new TestDataModel { id = id3, Name = "3" };
            List<TestDataModel> mockCollection = new List<TestDataModel> { item1, item2, item3 };

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<TestDataModel>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(predicate => mockCollection.Where(predicate));
            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(predicate => mockCollection.FirstOrDefault(predicate));
            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(id => mockCollection.FirstOrDefault(x => x.id == id));

            testDataService = new TestDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");
        }

        [Fact]
        public async Task Should_create_correct_add_event_for_add() {
            DataEventModel<TestDataModel> subject = null;

            mockDataCollection.Setup(x => x.AddAsync(It.IsAny<TestDataModel>())).Returns(Task.CompletedTask);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await testDataService.Add(item1);

            mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { id = id1, type = DataEventType.ADD, data = item1 });
        }

        [Fact]
        public async Task Should_create_correct_delete_event_for_delete() {
            DataEventModel<TestDataModel> subject = null;

            mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await testDataService.Delete(new TestDataModel { id = id1 });

            mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { id = id1, type = DataEventType.DELETE, data = null });
        }

        [Fact]
        public async Task Should_create_correct_delete_event_for_delete_by_id() {
            DataEventModel<TestDataModel> subject = null;

            mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await testDataService.Delete(id1);

            mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { id = id1, type = DataEventType.DELETE, data = null });
        }

        [Fact]
        public async Task Should_create_correct_delete_events_for_delete_many() {
            List<DataEventModel<TestDataModel>> subjects = new List<DataEventModel<TestDataModel>>();

            mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>())).Returns(Task.CompletedTask);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subjects.Add(dataEventModel));

            await testDataService.DeleteMany(x => x.Name == "1");

            mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Exactly(2));
            subjects.Should()
                    .BeEquivalentTo(
                        new DataEventModel<TestDataModel> { id = id1, type = DataEventType.DELETE, data = null },
                        new DataEventModel<TestDataModel> { id = id2, type = DataEventType.DELETE, data = null }
                    );
        }

        [Fact]
        public async Task Should_create_correct_update_event_for_replace() {
            DataEventModel<TestDataModel> subject = null;

            mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<TestDataModel>())).Returns(Task.CompletedTask);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subject = dataEventModel);

            await testDataService.Replace(item1);

            mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Once);
            subject.Should().BeEquivalentTo(new DataEventModel<TestDataModel> { id = id1, type = DataEventType.UPDATE, data = null });
        }

        [Fact]
        public async Task Should_create_correct_update_events_for_update_many() {
            List<DataEventModel<TestDataModel>> subjects = new List<DataEventModel<TestDataModel>>();

            mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subjects.Add(dataEventModel));

            await testDataService.UpdateMany(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

            mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Exactly(2));
            subjects.Should()
                    .BeEquivalentTo(
                        new DataEventModel<TestDataModel> { id = id1, type = DataEventType.UPDATE, data = null },
                        new DataEventModel<TestDataModel> { id = id2, type = DataEventType.UPDATE, data = null }
                    );
        }

        [Fact]
        public async Task Should_create_correct_update_events_for_updates() {
            List<DataEventModel<TestDataModel>> subjects = new List<DataEventModel<TestDataModel>>();

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);
            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<TestDataModel>>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>())).Callback<DataEventModel<TestDataModel>>(dataEventModel => subjects.Add(dataEventModel));

            await testDataService.Update(id1, "Name", "1");
            await testDataService.Update(id2, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));
            await testDataService.Update(x => x.id == id3, Builders<TestDataModel>.Update.Set(x => x.Name, "3"));

            mockDataEventBus.Verify(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()), Times.Exactly(3));
            subjects.Should()
                    .BeEquivalentTo(
                        new DataEventModel<TestDataModel> { id = id1, type = DataEventType.UPDATE, data = null },
                        new DataEventModel<TestDataModel> { id = id2, type = DataEventType.UPDATE, data = null },
                        new DataEventModel<TestDataModel> { id = id3, type = DataEventType.UPDATE, data = null }
                    );
        }
    }
}
