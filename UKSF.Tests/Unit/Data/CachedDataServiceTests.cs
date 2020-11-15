using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class CachedDataServiceTests {
        private readonly Mock<IDataCollection<TestDataModel>> mockDataCollection;
        private readonly Mock<IDataCollectionFactory> mockDataCollectionFactory;
        private readonly Mock<IDataEventBus<TestDataModel>> mockDataEventBus;
        private List<TestDataModel> mockCollection;
        private TestCachedDataService testCachedDataService;

        public CachedDataServiceTests() {
            mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockDataEventBus = new Mock<IDataEventBus<TestDataModel>>();
            mockDataCollection = new Mock<IDataCollection<TestDataModel>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<TestDataModel>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(() => new List<TestDataModel>(mockCollection));
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()));
        }

        [Fact]
        public void Should_cache_collection_when_null_for_get() {
            mockCollection = new List<TestDataModel>();

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            testCachedDataService.Cache.Should().BeNull();

            testCachedDataService.Get();

            testCachedDataService.Cache.Should().NotBeNull();
            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
        }

        [Fact]
        public void Should_cache_collection_when_null_for_get_single_by_id() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            TestDataModel subject = testCachedDataService.GetSingle(item2.id);

            testCachedDataService.Cache.Should().NotBeNull();
            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            subject.Should().Be(item2);
        }

        [Fact]
        public void Should_cache_collection_when_null_for_get_single_by_predicate() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            TestDataModel subject = testCachedDataService.GetSingle(x => x.Name == "2");

            testCachedDataService.Cache.Should().NotBeNull();
            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            subject.Should().Be(item2);
        }

        [Fact]
        public void Should_cache_collection_when_null_for_get_with_predicate() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            IEnumerable<TestDataModel> subject = testCachedDataService.Get(x => x.Name == "1");

            testCachedDataService.Cache.Should().NotBeNull();
            subject.Should().BeSubsetOf(testCachedDataService.Cache);
        }

        [Fact]
        public void Should_cache_collection_when_null_for_refresh() {
            mockCollection = new List<TestDataModel>();

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            testCachedDataService.Cache.Should().BeNull();

            testCachedDataService.Refresh();

            testCachedDataService.Cache.Should().NotBeNull();
            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
        }

        [Fact]
        public void Should_return_cached_collection_for_get() {
            mockCollection = new List<TestDataModel>();

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            testCachedDataService.Cache.Should().BeNull();

            List<TestDataModel> subject1 = testCachedDataService.Get().ToList();

            subject1.Should().NotBeNull();
            subject1.Should().BeEquivalentTo(mockCollection);

            List<TestDataModel> subject2 = testCachedDataService.Get().ToList();

            subject2.Should().NotBeNull();
            subject2.Should().BeEquivalentTo(mockCollection).And.BeEquivalentTo(subject1);
        }

        [Fact]
        public async Task Should_update_cache_for_add() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel>();

            mockDataCollection.Setup(x => x.AddAsync(It.IsAny<TestDataModel>())).Callback<TestDataModel>(x => mockCollection.Add(x));

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            testCachedDataService.Cache.Should().BeNull();

            await testCachedDataService.Add(item1);

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.Should().Contain(item1);
        }

        [Fact]
        public async Task Should_update_cache_for_delete_by_id() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            await testCachedDataService.Delete(item1.id);

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
        }

        [Fact]
        public async Task Should_update_cache_for_delete() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            await testCachedDataService.Delete(item1);

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
        }

        [Fact]
        public async Task Should_update_cache_for_delete_many() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "1" };
            TestDataModel item3 = new TestDataModel { Name = "3" };
            mockCollection = new List<TestDataModel> { item1, item2, item3 };

            mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((Expression<Func<TestDataModel, bool>> expression) => mockCollection.RemoveAll(x => mockCollection.Where(expression.Compile()).Contains(x)));

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            await testCachedDataService.DeleteMany(x => x.Name == "1");

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.Should().HaveCount(1);
            testCachedDataService.Cache.Should().Contain(item3);
        }

        [Fact]
        public async Task ShouldRefreshCollectionForReplace() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { id = item1.id, Name = "2" };
            mockCollection = new List<TestDataModel> { item1 };

            mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<TestDataModel>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, TestDataModel value) => mockCollection[mockCollection.FindIndex(x => x.id == id)] = value);

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            await testCachedDataService.Replace(item2);

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.First().Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldRefreshCollectionForUpdate() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1 };

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<TestDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            await testCachedDataService.Update(item1.id, "Name", "2");

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.First().Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldRefreshCollectionForUpdateByUpdateDefinition() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1 };

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<TestDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            await testCachedDataService.Update(item1.id, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.First().Name.Should().Be("2");
        }

        [Fact]
        public async Task ShouldRefreshCollectionForUpdateMany() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "1" };
            TestDataModel item3 = new TestDataModel { Name = "3" };
            mockCollection = new List<TestDataModel> { item1, item2, item3 };

            mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback(
                                  (Expression<Func<TestDataModel, bool>> expression, UpdateDefinition<TestDataModel> _) =>
                                      mockCollection.Where(expression.Compile()).ToList().ForEach(x => x.Name = "3")
                              );

            testCachedDataService = new TestCachedDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");

            await testCachedDataService.UpdateMany(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "3"));

            testCachedDataService.Cache.Should().BeEquivalentTo(mockCollection);
            testCachedDataService.Cache.ToList()[0].Name.Should().Be("3");
            testCachedDataService.Cache.ToList()[1].Name.Should().Be("3");
            testCachedDataService.Cache.ToList()[2].Name.Should().Be("3");
        }
    }
}
