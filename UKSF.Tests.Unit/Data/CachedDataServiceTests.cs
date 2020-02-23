using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        private readonly MockCachedDataService mockCachedDataService;
        private readonly Mock<IDataCollection> mockDataCollection;
        private List<MockDataModel> mockCollection;

        public CachedDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IMockCachedDataService>> mockDataEventBus = new Mock<IDataEventBus<IMockCachedDataService>>();
            mockDataCollection = new Mock<IDataCollection>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<IMockCachedDataService>>()));

            mockCachedDataService = new MockCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");
        }

        [Fact]
        public void ShouldCacheCollectionForGet() {
            mockCollection = new List<MockDataModel>();

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            mockCachedDataService.Collection.Should().BeNull();

            mockCachedDataService.Get();

            mockCachedDataService.Collection.Should().NotBeNull();
            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
        }

        [Fact]
        public void ShouldCacheCollectionForGetByPredicate() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "2" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            List<MockDataModel> subject = mockCachedDataService.Get(x => x.Name == "1");

            mockCachedDataService.Collection.Should().NotBeNull();
            subject.Should().BeSubsetOf(mockCachedDataService.Collection);
        }

        [Fact]
        public void ShouldCacheCollectionForGetSingle() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "2" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockDataModel subject = mockCachedDataService.GetSingle(item2.id);

            mockCachedDataService.Collection.Should().NotBeNull();
            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            subject.Should().Be(item2);
        }

        [Fact]
        public void ShouldCacheCollectionForGetSingleByPredicate() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "2" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            MockDataModel subject = mockCachedDataService.GetSingle(x => x.Name == "2");

            mockCachedDataService.Collection.Should().NotBeNull();
            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            subject.Should().Be(item2);
        }

        [Fact]
        public void ShouldCacheCollectionForRefreshWhenNull() {
            mockCollection = new List<MockDataModel>();

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            mockCachedDataService.Collection.Should().BeNull();

            mockCachedDataService.Refresh();

            mockCachedDataService.Collection.Should().NotBeNull();
            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
        }

        [Fact]
        public void ShouldGetCachedCollection() {
            mockCollection = new List<MockDataModel>();

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);

            mockCachedDataService.Collection.Should().BeNull();

            List<MockDataModel> subject1 = mockCachedDataService.Get();

            subject1.Should().NotBeNull();
            subject1.Should().BeSameAs(mockCollection);

            List<MockDataModel> subject2 = mockCachedDataService.Get();

            subject2.Should().NotBeNull();
            subject2.Should().BeSameAs(mockCollection).And.BeSameAs(subject1);
        }

        [Fact]
        public async Task ShouldRefreshCollectionForAdd() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel>();

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.AddAsync(It.IsAny<MockDataModel>())).Callback<MockDataModel>(x => mockCollection.Add(x));

            mockCachedDataService.Collection.Should().BeNull();

            await mockCachedDataService.Add(item1);

            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            mockCachedDataService.Collection.Should().Contain(item1);
        }

        [Fact]
        public async Task ShouldRefreshCollectionForDelete() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "2" };
            mockCollection = new List<MockDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.DeleteAsync<MockDataModel>(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            await mockCachedDataService.Delete(item1.id);

            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            mockCachedDataService.Collection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
        }

        [Fact]
        public async Task ShouldRefreshCollectionForDeleteMany() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "1" };
            MockDataModel item3 = new MockDataModel { Name = "3" };
            mockCollection = new List<MockDataModel> { item1, item2, item3 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<MockDataModel, bool>>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((Expression<Func<MockDataModel, bool>> expression) => mockCollection.RemoveAll(x => mockCollection.Where(expression.Compile()).Contains(x)));

            await mockCachedDataService.DeleteMany(x => x.Name == "1");

            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            mockCachedDataService.Collection.Should().HaveCount(1);
            mockCachedDataService.Collection.Should().Contain(item3);
        }

        [Fact]
        public async Task ShouldRefreshCollectionForReplace() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { id = item1.id, Name = "2" };
            mockCollection = new List<MockDataModel> { item1 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<MockDataModel>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, MockDataModel value) => mockCollection[mockCollection.FindIndex(x => x.id == id)] = value);

            await mockCachedDataService.Replace(item2);

            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            mockCachedDataService.Collection.First().Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldRefreshCollectionForUpdate() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            await mockCachedDataService.Update(item1.id, "Name", "2");

            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            mockCachedDataService.Collection.First().Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldRefreshCollectionForUpdateByUpdateDefinition() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            mockCollection = new List<MockDataModel> { item1 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<MockDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            await mockCachedDataService.Update(item1.id, Builders<MockDataModel>.Update.Set(x => x.Name, "2"));

            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            mockCachedDataService.Collection.First().Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldRefreshCollectionForUpdateMany() {
            MockDataModel item1 = new MockDataModel { Name = "1" };
            MockDataModel item2 = new MockDataModel { Name = "1" };
            MockDataModel item3 = new MockDataModel { Name = "3" };
            mockCollection = new List<MockDataModel> { item1, item2, item3 };

            mockDataCollection.Setup(x => x.Get<MockDataModel>()).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<MockDataModel, bool>>>(), It.IsAny<UpdateDefinition<MockDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback(
                                  (Expression<Func<MockDataModel, bool>> expression, UpdateDefinition<MockDataModel> _) =>
                                      mockCollection.Where(expression.Compile()).ToList().ForEach(x => x.Name = "3")
                              );

            await mockCachedDataService.UpdateMany(x => x.Name == "1", Builders<MockDataModel>.Update.Set(x => x.Name, "3"));

            mockCachedDataService.Collection.Should().BeSameAs(mockCollection);
            mockCachedDataService.Collection[0].Name.Should().Be("3");
            mockCachedDataService.Collection[1].Name.Should().Be("3");
            mockCachedDataService.Collection[2].Name.Should().Be("3");
        }
    }
}
