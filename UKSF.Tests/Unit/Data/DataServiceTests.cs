using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data;

public class DataServiceTests
{
    private readonly Mock<Api.Shared.Context.Base.IMongoCollection<TestDataModel>> _mockDataCollection;
    private readonly TestContext _testContext;
    private List<TestDataModel> _mockCollection;

    public DataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<TestDataModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

        _testContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, "test");
    }

    [Fact]
    public async Task Should_add_single_item()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new();

        _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<TestDataModel>())).Callback<TestDataModel>(x => _mockCollection.Add(x));

        await _testContext.Add(item1);

        _mockCollection.Should().Contain(item1);
    }

    [Fact]
    public async Task Should_delete_many_items()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "1" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns(() => _mockCollection);
        _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>()))
                           .Returns(Task.CompletedTask)
                           .Callback(
                               (Expression<Func<TestDataModel, bool>> expression) =>
                                   _mockCollection.RemoveAll(x => _mockCollection.Where(expression.Compile()).Contains(x))
                           );

        await _testContext.DeleteMany(x => x.Name == "1");

        _mockCollection.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_delete_single_item()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "2" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => _mockCollection.RemoveAll(x => x.Id == id));

        await _testContext.Delete(item1);

        _mockCollection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
    }

    [Fact]
    public async Task Should_delete_single_item_by_id()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "2" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => _mockCollection.RemoveAll(x => x.Id == id));

        await _testContext.Delete(item1.Id);

        _mockCollection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
    }

    [Fact]
    public void Should_get_all_items()
    {
        _mockDataCollection.Setup(x => x.Get()).Returns(() => _mockCollection);

        var subject = _testContext.Get();

        subject.Should().BeSameAs(_mockCollection);
    }

    [Fact]
    public void Should_get_items_matching_predicate()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "2" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(x => _mockCollection.Where(x).ToList());

        var subject = _testContext.Get(x => x.Id == item1.Id);

        subject.Should().HaveCount(1).And.Contain(item1);
    }

    [Fact]
    public void Should_get_single_item_by_id()
    {
        TestDataModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(item1);

        var subject = _testContext.GetSingle(item1.Id);

        subject.Should().Be(item1);
    }

    [Fact]
    public void Should_get_single_item_matching_predicate()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "2" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<TestDataModel, bool>>())).Returns<Func<TestDataModel, bool>>(x => _mockCollection.First(x));

        var subject = _testContext.GetSingle(x => x.Id == item1.Id);

        subject.Should().Be(item1);
    }

    [Fact]
    public async Task Should_replace_item()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Id = item1.Id, Name = "2" };
        _mockCollection = new() { item1 };

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(item1);
        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<TestDataModel>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, TestDataModel item) => _mockCollection[_mockCollection.FindIndex(x => x.Id == id)] = item);

        await _testContext.Replace(item2);

        _mockCollection.Should().ContainSingle();
        _mockCollection.First().Should().Be(item2);
    }

    [Fact]
    public async Task Should_throw_for_add_when_item_is_null()
    {
        var act = async () => await _testContext.Add(null);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Should_update_item_by_filter_and_update_definition()
    {
        TestDataModel item1 = new() { Id = "1", Name = "1" };
        _mockCollection = new() { item1 };
        var expectedFilter = Builders<TestDataModel>.Filter.Where(x => x.Name == "1").RenderFilter();
        var expectedUpdate = Builders<TestDataModel>.Update.Set(x => x.Name, "2").RenderUpdate();
        FilterDefinition<TestDataModel> subjectFilter = null;
        UpdateDefinition<TestDataModel> subjectUpdate = null;

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<TestDataModel, bool>>())).Returns(item1);
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<TestDataModel>>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback(
                               (FilterDefinition<TestDataModel> filter, UpdateDefinition<TestDataModel> update) =>
                               {
                                   subjectFilter = filter;
                                   subjectUpdate = update;
                               }
                           );

        await _testContext.Update(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

        subjectFilter.RenderFilter().Should().BeEquivalentTo(expectedFilter);
        subjectUpdate.RenderUpdate().Should().BeEquivalentTo(expectedUpdate);
    }

    [Fact]
    public async Task Should_update_item_by_id()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new() { item1 };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, UpdateDefinition<TestDataModel> _) => _mockCollection.First(x => x.Id == id).Name = "2");

        await _testContext.Update(item1.Id, x => x.Name, "2");

        item1.Name.Should().Be("2");
    }

    [Fact]
    public async Task Should_update_item_by_update_definition()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new() { item1 };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, UpdateDefinition<TestDataModel> _) => _mockCollection.First(x => x.Id == id).Name = "2");

        await _testContext.Update(item1.Id, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

        item1.Name.Should().Be("2");
    }

    [Fact]
    public async Task Should_update_item_with_set()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new() { item1 };
        var expected = Builders<TestDataModel>.Update.Set(x => x.Name, "2").RenderUpdate();
        UpdateDefinition<TestDataModel> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string _, UpdateDefinition<TestDataModel> y) => subject = y);

        await _testContext.Update(item1.Id, x => x.Name, "2");

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_update_item_with_unset()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new() { item1 };
        var expected = Builders<TestDataModel>.Update.Unset(x => x.Name).RenderUpdate();
        UpdateDefinition<TestDataModel> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string _, UpdateDefinition<TestDataModel> y) => subject = y);

        await _testContext.Update(item1.Id, x => x.Name, null);

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_update_many_items()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "1" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<TestDataModel, bool>>())).Returns(() => _mockCollection);
        _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback(
                               (Expression<Func<TestDataModel, bool>> expression, UpdateDefinition<TestDataModel> _) =>
                                   _mockCollection.Where(expression.Compile()).ToList().ForEach(y => y.Name = "2")
                           );

        await _testContext.UpdateMany(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

        item1.Name.Should().Be("2");
        item2.Name.Should().Be("2");
    }
}
