using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data;

public class CachedDataServiceTests
{
    private readonly Mock<Api.Core.Context.Base.IMongoCollection<TestDataModel>> _mockDataCollection;
    private readonly Mock<IMongoCollectionFactory> _mockDataCollectionFactory;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<IVariablesService> _mockIVariablesService = new();
    private List<TestDataModel> _mockCollection;
    private TestCachedContext _testCachedContext;

    public CachedDataServiceTests()
    {
        _mockDataCollectionFactory = new();
        _mockEventBus = new();
        _mockDataCollection = new();

        _mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<TestDataModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        _mockDataCollection.Setup(x => x.Get()).Returns(() => new List<TestDataModel>(_mockCollection));
        _mockIVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
    }

    [Fact]
    public void Should_cache_collection_for_new_context()
    {
        _mockCollection = new();

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Should_update_cache_for_add()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new();

        _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<TestDataModel>())).Callback<TestDataModel>(x => _mockCollection.Add(x));

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.Add(item1);

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_update_cache_for_delete()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "2" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => _mockCollection.RemoveAll(x => x.Id == id));

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.Delete(item1);

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_update_cache_for_delete_by_id()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "2" };
        _mockCollection = new() { item1, item2 };

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Callback((string id) => _mockCollection.RemoveAll(x => x.Id == id));

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.Delete(item1.Id);

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_update_cache_for_delete_many()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "1" };
        TestDataModel item3 = new() { Name = "3" };
        _mockCollection = new() { item1, item2, item3 };

        _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>()))
                           .Returns(Task.CompletedTask)
                           .Callback(
                               (Expression<Func<TestDataModel, bool>> expression) =>
                                   _mockCollection.RemoveAll(x => _mockCollection.Where(expression.Compile()).Contains(x))
                           );

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.DeleteMany(x => x.Name == "1");

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task ShouldRefreshCollectionForReplace()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Id = item1.Id, Name = "2" };
        _mockCollection = new() { item1 };

        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<TestDataModel>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, TestDataModel value) => _mockCollection[_mockCollection.FindIndex(x => x.Id == id)] = value);

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.Replace(item2);

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdate()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new() { item1 };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, UpdateDefinition<TestDataModel> _) => _mockCollection.First(x => x.Id == id).Name = "2");

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.Update(item1.Id, x => x.Name, "2");

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdateByUpdateDefinition()
    {
        TestDataModel item1 = new() { Name = "1" };
        _mockCollection = new() { item1 };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, UpdateDefinition<TestDataModel> _) => _mockCollection.First(x => x.Id == id).Name = "2");

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.Update(item1.Id, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdateMany()
    {
        TestDataModel item1 = new() { Name = "1" };
        TestDataModel item2 = new() { Name = "1" };
        TestDataModel item3 = new() { Name = "3" };
        _mockCollection = new() { item1, item2, item3 };

        _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask)
                           .Callback(
                               (Expression<Func<TestDataModel, bool>> expression, UpdateDefinition<TestDataModel> _) =>
                                   _mockCollection.Where(expression.Compile()).ToList().ForEach(x => x.Name = "3")
                           );

        _testCachedContext = new(_mockDataCollectionFactory.Object, _mockEventBus.Object, _mockIVariablesService.Object, "test");

        await _testCachedContext.UpdateMany(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "3"));

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }
}
