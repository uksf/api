using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class DataServiceTests {
        public DataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            mockDataEventBus = new Mock<IDataEventBus<IMockDataService>>();

            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<IMockDataService>>()));

            mockDataCollection.Setup(x => x.SetCollectionName(It.IsAny<string>()));
        }

        private readonly Mock<IDataCollection> mockDataCollection;
        private readonly Mock<IDataEventBus<IMockDataService>> mockDataEventBus;
        private List<MockDataModel> mockCollection;

        [Fact]
        public void ShouldMakeSetUpdate() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};
            const string EXPECTED = "{\"_operatorName\":\"$set\",\"_field\":{\"_fieldName\":\"Name\",\"_fieldSerializer\":null},\"_value\":\"2\"}";
            UpdateDefinition<MockDataModel> subject = null;

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>())).Returns(Task.CompletedTask).Callback((string x, UpdateDefinition<MockDataModel> y) => subject = y);

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockDataService.Update(item1.id, "Name", "2");

            subject.DeepJsonSerializeObject().Should().BeEquivalentTo(EXPECTED);
        }

        [Fact]
        public void ShouldMakeUnsetUpdate() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};
            const string EXPECTED = "{\"_operatorName\":\"$unset\",\"_field\":{\"_fieldName\":\"Name\"},\"_value\":1}";
            UpdateDefinition<MockDataModel> subject = null;

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>())).Returns(Task.CompletedTask).Callback((string x, UpdateDefinition<MockDataModel> y) => subject = y);

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockDataService.Update(item1.id, "Name", null);

            subject.DeepJsonSerializeObject().Should().BeEquivalentTo(EXPECTED);
        }

        [Fact]
        public void ShouldAddItem() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());
            mockDataCollection.Setup(x => x.Add(It.IsAny<MockDataModel>())).Callback<MockDataModel>(x => mockCollection.Add(x));

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockDataService.Add(item1);

            mockCollection.Should().Contain(item1);
        }

        [Fact]
        public void ShouldCreateCollection() {
            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());

            mockCollection.Should().BeNull();
            MockDataService unused = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockCollection.Should().NotBeNull();
        }

        [Fact]
        public void ShouldDeleteItem() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};
            
            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Delete<MockDataModel>(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockDataService.Delete(item1.id);
            
            mockCollection.Should().HaveCount(0);
            mockCollection.Should().NotContain(item1);
        }

        [Fact]
        public void ShouldGetItem() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());
            mockDataCollection.Setup(x => x.GetSingle<MockDataModel>(It.IsAny<string>())).Returns(item1);

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            MockDataModel subject = mockDataService.GetSingle(item1.id);

            subject.Should().Be(item1);
        }

        [Fact]
        public void ShouldGetItemByPredicate() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};
            MockDataModel item2 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "2"};
            string id = item1.id;

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1, item2});
            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<MockDataModel, bool>>())).Returns<Func<MockDataModel, bool>>(x => mockCollection.First(x));

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            MockDataModel subject = mockDataService.GetSingle(x => x.id == id);

            subject.Should().Be(item1);
        }

        [Fact]
        public void ShouldGetItems() {
            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            List<MockDataModel> subject = mockDataService.Get();

            subject.Should().BeSameAs(mockCollection);
        }

        [Fact]
        public void ShouldGetItemsByPredicate() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};
            MockDataModel item2 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "2"};
            string id = item1.id;

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1, item2});
            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<MockDataModel, bool>>())).Returns<Func<MockDataModel, bool>>(x => mockCollection.Where(x).ToList());

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            List<MockDataModel> subject = mockDataService.Get(x => x.id == id);

            subject.Should().HaveCount(1);
            subject.Should().Contain(item1);
        }

        [Fact]
        public void ShouldSetCollectionName() {
            string collectionName = "";

            mockDataCollection.Setup(x => x.SetCollectionName(It.IsAny<string>())).Callback((string x) => collectionName = x);
            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());

            MockDataService unused = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            collectionName.Should().Be("test");
        }

        [Fact]
        public void ShouldUpdateItemValue() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>())).Returns(Task.CompletedTask).Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockDataService.Update(item1.id, "Name", "2");

            item1.Name.Should().Be("2");
        }

        [Fact]
        public void ShouldUpdateItemValueByUpdateDefinition() {
            MockDataModel item1 = new MockDataModel(ObjectId.GenerateNewId().ToString()) {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>())).Returns(Task.CompletedTask).Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockDataService.Update(item1.id, Builders<MockDataModel>.Update.Set(x => x.Name, "2"));

            item1.Name.Should().Be("2");
        }
    }
}
