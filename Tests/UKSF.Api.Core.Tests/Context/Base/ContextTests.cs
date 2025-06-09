using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Base;

public class ContextTests
{
    private readonly Mock<Core.Context.Base.IMongoCollection<DomainTestModel>> _mockDataCollection;
    private readonly TestContext _testContext;
    private List<DomainTestModel> _mockCollection;

    public ContextTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new Mock<Core.Context.Base.IMongoCollection<DomainTestModel>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainTestModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

        _testContext = new TestContext(mockDataCollectionFactory.Object, mockEventBus.Object, "test");
    }

    [Fact]
    public async Task Should_add_single_item()
    {
        DomainTestModel item1 = new() { Name = "1" };
        _mockCollection = [];

        _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<DomainTestModel>())).Callback<DomainTestModel>(x => _mockCollection.Add(x));

        await _testContext.Add(item1);

        _mockCollection.Should().Contain(item1);
    }

    [Fact]
    public async Task Should_delete_many_items()
    {
        DomainTestModel item1 = new() { Name = "1" };
        DomainTestModel item2 = new() { Name = "1" };
        _mockCollection = [item1, item2];

        _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<DomainTestModel, bool>>())).Returns(() => _mockCollection);
        _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((Expression<Func<DomainTestModel, bool>> expression) =>
                                         _mockCollection.RemoveAll(x => _mockCollection.Where(expression.Compile()).Contains(x))
                           );

        await _testContext.DeleteMany(x => x.Name == "1");

        _mockCollection.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_delete_single_item()
    {
        DomainTestModel item1 = new() { Name = "1" };
        DomainTestModel item2 = new() { Name = "2" };
        _mockCollection = [item1, item2];

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => _mockCollection.RemoveAll(x => x.Id == id));

        await _testContext.Delete(item1);

        _mockCollection.Should().HaveCount(1).And.NotContain(item1).And.Contain(item2);
    }

    [Fact]
    public async Task Should_delete_single_item_by_id()
    {
        DomainTestModel item1 = new() { Name = "1" };
        DomainTestModel item2 = new() { Name = "2" };
        _mockCollection = [item1, item2];

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
        DomainTestModel item1 = new() { Name = "1" };
        DomainTestModel item2 = new() { Name = "2" };
        _mockCollection = [item1, item2];

        _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<DomainTestModel, bool>>()))
                           .Returns<Func<DomainTestModel, bool>>(x => _mockCollection.Where(x).ToList());

        var subject = _testContext.Get(x => x.Id == item1.Id);

        subject.Should().HaveCount(1).And.Contain(item1);
    }

    [Fact]
    public void Should_get_single_item_by_id()
    {
        DomainTestModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(item1);

        var subject = _testContext.GetSingle(item1.Id);

        subject.Should().Be(item1);
    }

    [Fact]
    public void Should_get_single_item_matching_predicate()
    {
        DomainTestModel item1 = new() { Name = "1" };
        DomainTestModel item2 = new() { Name = "2" };
        _mockCollection = [item1, item2];

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<DomainTestModel, bool>>()))
                           .Returns<Func<DomainTestModel, bool>>(x => _mockCollection.First(x));

        var subject = _testContext.GetSingle(x => x.Id == item1.Id);

        subject.Should().Be(item1);
    }

    [Fact]
    public async Task Should_replace_item()
    {
        DomainTestModel item1 = new() { Name = "1" };
        DomainTestModel item2 = new() { Id = item1.Id, Name = "2" };
        _mockCollection = [item1];

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(item1);
        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<DomainTestModel>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, DomainTestModel item) => _mockCollection[_mockCollection.FindIndex(x => x.Id == id)] = item);

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
        DomainTestModel item1 = new() { Id = "1", Name = "1" };
        _mockCollection = [item1];
        var expectedFilter = Builders<DomainTestModel>.Filter.Where(x => x.Name == "1").RenderFilter();
        var expectedUpdate = Builders<DomainTestModel>.Update.Set(x => x.Name, "2").RenderUpdate();
        FilterDefinition<DomainTestModel> subjectFilter = null;
        UpdateDefinition<DomainTestModel> subjectUpdate = null;

        _mockDataCollection.Setup(x => x.GetSingle(It.IsAny<Func<DomainTestModel, bool>>())).Returns(item1);
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<DomainTestModel>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((FilterDefinition<DomainTestModel> filter, UpdateDefinition<DomainTestModel> update) =>
                               {
                                   subjectFilter = filter;
                                   subjectUpdate = update;
                               }
                           );

        await _testContext.Update(x => x.Name == "1", Builders<DomainTestModel>.Update.Set(x => x.Name, "2"));

        subjectFilter.RenderFilter().Should().BeEquivalentTo(expectedFilter);
        subjectUpdate.RenderUpdate().Should().BeEquivalentTo(expectedUpdate);
    }

    [Fact]
    public async Task Should_update_item_by_id()
    {
        DomainTestModel item1 = new() { Name = "1" };
        _mockCollection = [item1];

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, UpdateDefinition<DomainTestModel> _) => _mockCollection.First(x => x.Id == id).Name = "2");

        await _testContext.Update(item1.Id, x => x.Name, "2");

        item1.Name.Should().Be("2");
    }

    [Fact]
    public async Task Should_update_item_by_update_definition()
    {
        DomainTestModel item1 = new() { Name = "1" };
        _mockCollection = [item1];

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, UpdateDefinition<DomainTestModel> _) => _mockCollection.First(x => x.Id == id).Name = "2");

        await _testContext.Update(item1.Id, Builders<DomainTestModel>.Update.Set(x => x.Name, "2"));

        item1.Name.Should().Be("2");
    }

    [Fact]
    public async Task Should_update_item_with_set()
    {
        DomainTestModel item1 = new() { Name = "1" };
        _mockCollection = [item1];
        var expected = Builders<DomainTestModel>.Update.Set(x => x.Name, "2").RenderUpdate();
        UpdateDefinition<DomainTestModel> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string _, UpdateDefinition<DomainTestModel> y) => subject = y);

        await _testContext.Update(item1.Id, x => x.Name, "2");

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_update_item_with_unset()
    {
        DomainTestModel item1 = new() { Name = "1" };
        _mockCollection = [item1];
        var expected = Builders<DomainTestModel>.Update.Unset(x => x.Name).RenderUpdate();
        UpdateDefinition<DomainTestModel> subject = null;

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string _, UpdateDefinition<DomainTestModel> y) => subject = y);

        await _testContext.Update(item1.Id, x => x.Name, null);

        subject.RenderUpdate().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_update_many_items()
    {
        DomainTestModel item1 = new() { Name = "1" };
        DomainTestModel item2 = new() { Name = "1" };
        _mockCollection = [item1, item2];

        _mockDataCollection.Setup(x => x.Get(It.IsAny<Func<DomainTestModel, bool>>())).Returns(() => _mockCollection);
        _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((Expression<Func<DomainTestModel, bool>> expression, UpdateDefinition<DomainTestModel> _) =>
                                         _mockCollection.Where(expression.Compile()).ToList().ForEach(y => y.Name = "2")
                           );

        await _testContext.UpdateMany(x => x.Name == "1", Builders<DomainTestModel>.Update.Set(x => x.Name, "2"));

        item1.Name.Should().Be("2");
        item2.Name.Should().Be("2");
    }
}
