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

namespace UKSF.Api.Core.Tests.Context.Base;

public class CachedContextTests
{
    private readonly Mock<Core.Context.Base.IMongoCollection<DomainTestModel>> _mockDataCollection;
    private readonly Mock<IVariablesService> _mockIVariablesService = new();
    private readonly TestCachedContext _testCachedContext;

    public CachedContextTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new Mock<Core.Context.Base.IMongoCollection<DomainTestModel>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainTestModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        _mockDataCollection.Setup(x => x.Get()).Returns(() => new List<DomainTestModel>());
        _mockIVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _testCachedContext = new TestCachedContext(mockDataCollectionFactory.Object, mockEventBus.Object, _mockIVariablesService.Object, "test");
    }

    [Fact]
    public async Task Should_update_cache_for_add()
    {
        _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);

        await _testCachedContext.Add(new DomainTestModel { Name = "1" });

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Should_update_cache_for_delete()
    {
        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _testCachedContext.Delete(new DomainTestModel { Name = "1" });

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Should_update_cache_for_delete_by_id()
    {
        DomainTestModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _testCachedContext.Delete(item1.Id);

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Should_update_cache_for_delete_many()
    {
        _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>())).Returns(Task.CompletedTask);

        await _testCachedContext.DeleteMany(x => x.Name == "1");

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task ShouldRefreshCollectionForReplace()
    {
        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);

        await _testCachedContext.Replace(new DomainTestModel { Name = "1" });

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdate()
    {
        DomainTestModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);

        await _testCachedContext.Update(item1.Id, x => x.Name, "2");

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdateByUpdateDefinition()
    {
        DomainTestModel item1 = new() { Name = "1" };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);

        await _testCachedContext.Update(item1.Id, Builders<DomainTestModel>.Update.Set(x => x.Name, "2"));

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task ShouldRefreshCollectionForUpdateMany()
    {
        _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);

        await _testCachedContext.UpdateMany(x => x.Name == "1", Builders<DomainTestModel>.Update.Set(x => x.Name, "3"));

        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }
}
