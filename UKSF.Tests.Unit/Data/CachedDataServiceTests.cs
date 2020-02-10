using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class CachedDataServiceTests {
        private readonly Mock<IDataCollection> mockDataCollection;
        private readonly Mock<IDataEventBus<IMockCachedDataService>> mockDataEventBus;
        private List<MockDataModel> mockCollection;

        public CachedDataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            mockDataEventBus = new Mock<IDataEventBus<IMockCachedDataService>>();

            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<IMockCachedDataService>>()));

            mockDataCollection.Setup(x => x.SetCollectionName(It.IsAny<string>()));
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldGetNothingWhenNoIdOrNull(string id) {
            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockCachedDataService mockCachedDataService = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            MockDataModel subject = mockCachedDataService.GetSingle(id);

            subject.Should().Be(null);
        }

        [Fact]
        public async Task ShouldAddItem() {
            MockDataModel item1 = new MockDataModel {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.Add(It.IsAny<MockDataModel>())).Callback<MockDataModel>(x => mockCollection.Add(x));

            MockCachedDataService subject = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            await subject.Add(item1);

            subject.Collection.Should().Contain(item1);
        }

        [Fact]
        public async Task ShouldDeleteItem() {
            MockDataModel item1 = new MockDataModel {Name = "1"};
            MockDataModel item2 = new MockDataModel {Name = "2"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1, item2});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.Delete<MockDataModel>(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            MockCachedDataService subject = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            await subject.Delete(item1.id);

            subject.Collection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
        }

        [Fact]
        public void ShouldGetCachedItem() {
            MockDataModel item1 = new MockDataModel {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockCachedDataService mockCachedDataService = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            MockDataModel subject = mockCachedDataService.GetSingle(item1.id);

            subject.Should().Be(item1);
        }

        [Fact]
        public void ShouldGetCachedItemByPredicate() {
            MockDataModel item1 = new MockDataModel {Name = "1"};
            string id = item1.id;

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockCachedDataService mockCachedDataService = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            MockDataModel subject = mockCachedDataService.GetSingle(x => x.id == id);

            subject.Should().Be(item1);
        }

        [Fact]
        public void ShouldGetCachedItems() {
            MockDataModel item1 = new MockDataModel {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockCachedDataService mockCachedDataService = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockCachedDataService.Refresh();
            List<MockDataModel> subject = mockCachedDataService.Get();

            subject.Should().BeSameAs(mockCollection);
        }

        [Fact]
        public void ShouldGetCachedItemsByPredicate() {
            MockDataModel item1 = new MockDataModel {Name = "1"};
            MockDataModel item2 = new MockDataModel {Name = "2"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1, item2});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockCachedDataService mockCachedDataService = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            List<MockDataModel> subject = mockCachedDataService.Get(x => x.Name == "1");

            subject.Should().Contain(item1);
        }

        [Fact]
        public void ShouldRefreshCollection() {
            MockDataModel item1 = new MockDataModel {Name = "1"};
            MockDataModel item2 = new MockDataModel {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockCachedDataService subject = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            mockCollection.Add(item2);

            subject.Refresh();
            subject.Collection.Should().Contain(item1);
            subject.Collection.Should().Contain(item2);
        }

        [Fact]
        public async Task ShouldUpdateItemValue() {
            MockDataModel item1 = new MockDataModel {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>())).Returns(Task.CompletedTask).Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            MockCachedDataService subject = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            await subject.Update(item1.id, "Name", "2");

            subject.Collection.First().Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldUpdateItemValueByUpdateDefinition() {
            MockDataModel item1 = new MockDataModel {Name = "1"};

            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel> {item1});
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>())).Returns(Task.CompletedTask).Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            MockCachedDataService subject = new MockCachedDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            await subject.Update(item1.id, Builders<MockDataModel>.Update.Set(x => x.Name, "2"));

            subject.Collection.First().Name.Should().Be("2");
        }
    }
}
