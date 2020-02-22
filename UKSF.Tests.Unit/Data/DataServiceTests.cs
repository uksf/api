using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class DataServiceTests {
        private readonly Mock<IDataCollection> mockDataCollection;
        private readonly Mock<IDataEventBus<IMockDataService>> mockDataEventBus;
        private readonly MockDataService mockDataService;
        private List<MockDataModel> mockCollection;

        public DataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            mockDataEventBus = new Mock<IDataEventBus<IMockDataService>>();

            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<IMockDataService>>()));
            mockDataCollection.Setup(x => x.AssertCollectionExists<MockDataModel>()).Callback(() => mockCollection = new List<MockDataModel>());
            mockDataCollection.Setup(x => x.SetCollectionName(It.IsAny<string>()));

            mockDataService = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldThrowForDeleteWhenNoKeyOrNull(string id) {

            Func<Task> act = async () => await mockDataService.Delete(id);

            act.Should().Throw<KeyNotFoundException>();
        }

        [Fact]
        public void ShouldThrowForDeleteManyWhenNoMatchingItems() {
            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<MockDataModel, bool>>())).Returns(new List<MockDataModel>());
            Func<Task> act = async () => await mockDataService.DeleteMany(null);

            act.Should().Throw<KeyNotFoundException>();
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldGetNothingWhenNoKeyOrNull(string id) {
            MockDataModel subject = mockDataService.GetSingle(id);

            subject.Should().Be(null);
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldThrowForUpdateWhenNoKeyOrNull(string id) {
            Func<Task> act = async () => await mockDataService.Update(id, "Name", null);

            act.Should().Throw<KeyNotFoundException>();
        }

        [Fact]
        public void ShouldThrowForReplaceWhenItemNotFound() {
            MockDataModel item = new MockDataModel { Name = "1" };

            Func<Task> act = async () => await mockDataService.Replace(item);

            act.Should().Throw<KeyNotFoundException>();
        }

        [Fact]
        public void ShouldThrowForUpdateManyWhenNoMatchingItems() {
            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<MockDataModel, bool>>())).Returns(new List<MockDataModel>());
            Func<Task> act = async () => await mockDataService.UpdateMany(null, null);

            act.Should().Throw<KeyNotFoundException>();
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldThrowForUpdateWithUpdateDefinitionWhenNoKeyOrNull(string id) {
            Func<Task> act = async () => await mockDataService.Update(id, Builders<MockDataModel>.Update.Set(x => x.Name, "2"));

            act.Should().Throw<KeyNotFoundException>();
        }

        [Fact]
        public async Task ShouldAddItem() {
            MockDataModel item1 = new MockDataModel { Name = "1" };

            mockDataCollection.Setup(x => x.Add(It.IsAny<MockDataModel>())).Callback<MockDataModel>(x => mockCollection.Add(x));

            await mockDataService.Add(item1);

            mockCollection.Should().Contain(item1);
        }

        [Fact]
        public async Task ShouldDeleteItem() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "2" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Delete<MockDataModel>(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            await mockDataService.Delete(item1.id);

            mockCollection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
        }

        [Fact]
        public void ShouldGetItem() {
            MockDataModel item1 = new MockDataModel { Name = "1" };

            mockDataCollection.Setup(x => x.GetSingle<MockDataModel>(It.IsAny<string>())).Returns(item1);

            MockDataModel subject = mockDataService.GetSingle(item1.id);

            subject.Should().Be(item1);
        }

        [Fact]
        public void ShouldGetItemByPredicate() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "2" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<MockDataModel, bool>>())).Returns<Func<MockDataModel, bool>>(x => mockCollection.First(x));

            MockDataModel subject = mockDataService.GetSingle(x => x.id == item1.id);

            subject.Should().Be(item1);
        }

        [Fact]
        public void ShouldGetItems() {
            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            List<MockDataModel> subject = mockDataService.Get();

            subject.Should().BeSameAs(mockCollection);
        }

        [Fact]
        public void ShouldGetItemsByPredicate() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "2" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<MockDataModel, bool>>())).Returns<Func<MockDataModel, bool>>(x => mockCollection.Where(x).ToList());

            List<MockDataModel> subject = mockDataService.Get(x => x.id == item1.id);

            subject.Should().HaveCount(1).And.Contain(item1);
        }

        [Fact]
        public async Task ShouldMakeSetUpdate() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1 };
            BsonValue expected = TestUtilities.Render(Builders<MockDataModel>.Update.Set(x => x.Name, "2"));
            UpdateDefinition<MockDataModel> subject = null;

            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string x, UpdateDefinition<MockDataModel> y) => subject = y);

            await mockDataService.Update(item1.id, "Name", "2");

            TestUtilities.Render(subject).Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task ShouldMakeUnsetUpdate() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1 };
            BsonValue expected = TestUtilities.Render(Builders<MockDataModel>.Update.Unset(x => x.Name));
            UpdateDefinition<MockDataModel> subject = null;

            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string x, UpdateDefinition<MockDataModel> y) => subject = y);

            await mockDataService.Update(item1.id, "Name", null);

            TestUtilities.Render(subject).Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void ShouldSetCollectionName() {
            string collectionName = "";

            mockDataCollection.Setup(x => x.SetCollectionName(It.IsAny<string>())).Callback((string x) => collectionName = x);

            MockDataService unused = new MockDataService(mockDataCollection.Object, mockDataEventBus.Object, "test");
            collectionName.Should().Be("test");
        }

        [Fact]
        public void ShouldThrowForAddWhenItemIsNull() {
            Func<Task> act = async () => await mockDataService.Add(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task ShouldUpdateItemValue() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1 };

            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            await mockDataService.Update(item1.id, "Name", "2");

            item1.Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldReplaceItem() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { id = item1.id, Name = "2" };
            mockCollection = new List<MockDataModel> { item1 };

            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<MockDataModel, bool>>())).Returns(item1);
            mockDataCollection.Setup(x => x.Replace(It.IsAny<string>(), It.IsAny<MockDataModel>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, MockDataModel item) => mockCollection[mockCollection.FindIndex(x => x.id == id)] = item);

            await mockDataService.Replace(item2);

            mockCollection.Should().ContainSingle();
            mockCollection.First().Should().Be(item2);
        }

        [Fact]
        public async Task ShouldUpdateItemValueByUpdateDefinition() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1 };

            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            await mockDataService.Update(item1.id, Builders<MockDataModel>.Update.Set(x => x.Name, "2"));

            item1.Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldUpdateMany() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<MockDataModel, bool>>())).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.UpdateMany(It.IsAny<Expression<Func<MockDataModel, bool>>>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((Expression<Func<MockDataModel, bool>> expression, UpdateDefinition<MockDataModel> _) => mockCollection.Where(expression.Compile()).ToList().ForEach(y => y.Name = "2"));

            await mockDataService.UpdateMany(x => x.Name == "1", Builders<MockDataModel>.Update.Set(x => x.Name, "2"));

            item1.Name.Should().Be("2");
            item2.Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldDeleteMany() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<MockDataModel, bool>>())).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<MockDataModel, bool>>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((Expression<Func<MockDataModel, bool>> expression) => mockCollection.RemoveAll(x => mockCollection.Where(expression.Compile()).Contains(x)));

            await mockDataService.DeleteMany(x => x.Name == "1");

            mockCollection.Should().BeEmpty();
        }
    }
}
