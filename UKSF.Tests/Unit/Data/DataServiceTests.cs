using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class DataServiceTests {
        private readonly Mock<IDataCollection<TestDataModel>> mockDataCollection;
        private readonly TestDataService testDataService;
        private List<TestDataModel> mockCollection;

        public DataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<TestDataModel>> mockDataEventBus = new Mock<IDataEventBus<TestDataModel>>();
            mockDataCollection = new Mock<IDataCollection<TestDataModel>>();

            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<TestDataModel>>()));
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<TestDataModel>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            testDataService = new TestDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object, "test");
        }

        [Theory, InlineData(""), InlineData("1"), InlineData(null)]
        public async Task Should_throw_for_delete_single_item_when_key_is_invalid(string id) {
            Func<Task> act = async () => await testDataService.Delete(id);

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Theory, InlineData(""), InlineData("1"), InlineData(null)]
        public void Should_throw_for_get_single_item_when_key_is_invalid(string id) {
            Action act = () => testDataService.GetSingle(id);

            act.Should().Throw<KeyNotFoundException>();
        }

        [Theory, InlineData(""), InlineData("1"), InlineData(null)]
        public async Task Should_throw_for_update_by_id_when_key_is_invalid(string id) {
            Func<Task> act = async () => await testDataService.Update(id, "Name", null);

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Theory, InlineData(""), InlineData("1"), InlineData(null)]
        public async Task Should_throw_for_update_by_update_definition_when_key_is_invalid(string id) {
            Func<Task> act = async () => await testDataService.Update(id, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task Should_add_single_item() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel>();

            mockDataCollection.Setup(x => x.AddAsync(It.IsAny<TestDataModel>())).Callback<TestDataModel>(x => mockCollection.Add(x));

            await testDataService.Add(item1);

            mockCollection.Should().Contain(item1);
        }

        [Fact]
        public async Task Should_delete_many_items() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((Expression<Func<TestDataModel, bool>> expression) => mockCollection.RemoveAll(x => mockCollection.Where(expression.Compile()).Contains(x)));

            await testDataService.DeleteMany(x => x.Name == "1");

            mockCollection.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_delete_single_item_by_id() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            await testDataService.Delete(item1.id);

            mockCollection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
        }

        [Fact]
        public async Task Should_delete_single_item() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            await testDataService.Delete(item1);

            mockCollection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
        }

        [Fact]
        public void Should_get_all_items() {
            mockDataCollection.Setup(x => x.Get()).Returns(() => mockCollection);

            IEnumerable<TestDataModel> subject = testDataService.Get();

            subject.Should().BeSameAs(mockCollection);
        }

        [Fact]
        public void Should_get_items_matching_predicate() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(x => mockCollection.Where(x).ToList());

            IEnumerable<TestDataModel> subject = testDataService.Get(x => x.id == item1.id);

            subject.Should().HaveCount(1).And.Contain(item1);
        }

        [Fact]
        public void Should_get_single_item_by_id() {
            TestDataModel item1 = new TestDataModel { Name = "1" };

            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(item1);

            TestDataModel subject = testDataService.GetSingle(item1.id);

            subject.Should().Be(item1);
        }

        [Fact]
        public void Should_get_single_item_matching_predicate() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "2" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(x => mockCollection.First(x));

            TestDataModel subject = testDataService.GetSingle(x => x.id == item1.id);

            subject.Should().Be(item1);
        }

        [Fact]
        public async Task Should_replace_item() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { id = item1.id, Name = "2" };
            mockCollection = new List<TestDataModel> { item1 };

            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(item1);
            mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<TestDataModel>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, TestDataModel item) => mockCollection[mockCollection.FindIndex(x => x.id == id)] = item);

            await testDataService.Replace(item2);

            mockCollection.Should().ContainSingle();
            mockCollection.First().Should().Be(item2);
        }

        [Fact]
        public async Task Should_throw_for_add_when_item_is_null() {
            Func<Task> act = async () => await testDataService.Add(null);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task Should_update_item_by_filter_and_update_definition() {
            TestDataModel item1 = new TestDataModel { id = "1", Name = "1" };
            mockCollection = new List<TestDataModel> { item1 };
            BsonValue expectedFilter = TestUtilities.RenderFilter(Builders<TestDataModel>.Filter.Where(x => x.Name == "1"));
            BsonValue expectedUpdate = TestUtilities.RenderUpdate(Builders<TestDataModel>.Update.Set(x => x.Name, "2"));
            FilterDefinition<TestDataModel> subjectFilter = null;
            UpdateDefinition<TestDataModel> subjectUpdate = null;

            mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<TestDataModel, bool>>())).Returns(item1);
            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<TestDataModel>>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback(
                                  (FilterDefinition<TestDataModel> filter, UpdateDefinition<TestDataModel> update) => {
                                      subjectFilter = filter;
                                      subjectUpdate = update;
                                  }
                              );

            await testDataService.Update(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

            TestUtilities.RenderFilter(subjectFilter).Should().BeEquivalentTo(expectedFilter);
            TestUtilities.RenderUpdate(subjectUpdate).Should().BeEquivalentTo(expectedUpdate);
        }

        [Fact]
        public async Task Should_update_item_by_id() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1 };

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<TestDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            await testDataService.Update(item1.id, "Name", "2");

            item1.Name.Should().Be("2");
        }

        [Fact]
        public async Task Should_update_item_by_update_definition() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1 };

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<TestDataModel> _) => mockCollection.First(x => x.id == id).Name = "2");

            await testDataService.Update(item1.id, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

            item1.Name.Should().Be("2");
        }

        [Fact]
        public async Task Should_update_item_with_set() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1 };
            BsonValue expected = TestUtilities.RenderUpdate(Builders<TestDataModel>.Update.Set(x => x.Name, "2"));
            UpdateDefinition<TestDataModel> subject = null;

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string x, UpdateDefinition<TestDataModel> y) => subject = y);

            await testDataService.Update(item1.id, "Name", "2");

            TestUtilities.RenderUpdate(subject).Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task Should_update_item_with_unset() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1 };
            BsonValue expected = TestUtilities.RenderUpdate(Builders<TestDataModel>.Update.Unset(x => x.Name));
            UpdateDefinition<TestDataModel> subject = null;

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string x, UpdateDefinition<TestDataModel> y) => subject = y);

            await testDataService.Update(item1.id, "Name", null);

            TestUtilities.RenderUpdate(subject).Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task Should_update_many_items() {
            TestDataModel item1 = new TestDataModel { Name = "1" };
            TestDataModel item2 = new TestDataModel { Name = "1" };
            mockCollection = new List<TestDataModel> { item1, item2 };

            mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns(() => mockCollection);
            mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                              .Returns(Task.CompletedTask)
                              .Callback(
                                  (Expression<Func<TestDataModel, bool>> expression, UpdateDefinition<TestDataModel> _) =>
                                      mockCollection.Where(expression.Compile()).ToList().ForEach(y => y.Name = "2")
                              );

            await testDataService.UpdateMany(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

            item1.Name.Should().Be("2");
            item2.Name.Should().Be("2");
        }
    }
}
