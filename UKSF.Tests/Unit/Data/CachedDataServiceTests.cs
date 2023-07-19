using System;
using System.Collections.Generic;
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
    private readonly Mock<IVariablesService> _mockIVariablesService = new();
    private readonly TestCachedContext _testCachedContext;

    public CachedDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<TestDataModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        _mockDataCollection.Setup(x => x.Get()).Returns(() => new List<TestDataModel>());
        _mockIVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _testCachedContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, _mockIVariablesService.Object, "test");
    }

    [Fact]
    public async Task Should_update_cache_for_add()
    {
        _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<TestDataModel>())).Returns(Task.CompletedTask);

        await _testCachedContext.Add(new() { Name = "1" });

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Should_update_cache_for_delete()
    {
        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _testCachedContext.Delete((TestDataModel)new() { Name = "1" });

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Should_update_cache_for_delete_by_id()
    {
        TestDataModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _testCachedContext.Delete(item1.Id);

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Should_update_cache_for_delete_many()
    {
        _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>())).Returns(Task.CompletedTask);

        await _testCachedContext.DeleteMany(x => x.Name == "1");

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task ShouldRefreshCollectionForReplace()
    {
        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<TestDataModel>())).Returns(Task.CompletedTask);

        await _testCachedContext.Replace(new() { Name = "1" });

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdate()
    {
        TestDataModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);

        await _testCachedContext.Update(item1.Id, x => x.Name, "2");

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdateByUpdateDefinition()
    {
        TestDataModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<TestDataModel>>())).Returns(Task.CompletedTask);

        await _testCachedContext.Update(item1.Id, Builders<TestDataModel>.Update.Set(x => x.Name, "2"));

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdateMany()
    {
        _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<TestDataModel, bool>>>(), It.IsAny<UpdateDefinition<TestDataModel>>()))
                           .Returns(Task.CompletedTask);

        await _testCachedContext.UpdateMany(x => x.Name == "1", Builders<TestDataModel>.Update.Set(x => x.Name, "3"));

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }
}
