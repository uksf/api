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
    public async Task Add_ShouldFullRefresh()
    {
        _mockDataCollection.Setup(x => x.AddAsync(It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);

        await _testCachedContext.Add(new DomainTestModel { Name = "1" });

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Delete_ByItem_ShouldNotFullRefresh()
    {
        var item = new DomainTestModel { Name = "1" };
        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _testCachedContext.Delete(item);

        _mockDataCollection.Verify(x => x.Get(), Times.Never);
    }

    [Fact]
    public async Task Delete_ById_ShouldNotFullRefresh()
    {
        var item = new DomainTestModel { Name = "1" };
        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _testCachedContext.Delete(item.Id);

        _mockDataCollection.Verify(x => x.Get(), Times.Never);
    }

    [Fact]
    public async Task Delete_ById_ShouldRemoveItemFromCache()
    {
        var item = new DomainTestModel { Name = "1" };
        _mockDataCollection.Setup(x => x.Get()).Returns(() => new List<DomainTestModel> { item });
        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        // Initialize cache
        _testCachedContext.Get().Should().HaveCount(1);

        await _testCachedContext.Delete(item.Id);

        _testCachedContext.Get().Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteMany_ShouldFullRefresh()
    {
        _mockDataCollection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>())).Returns(Task.CompletedTask);

        await _testCachedContext.DeleteMany(x => x.Name == "1");

        // Once for Get(predicate) to collect IDs, once for Refresh after delete
        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task Replace_ShouldNotFullRefresh()
    {
        var item = new DomainTestModel { Name = "1" };
        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);

        await _testCachedContext.Replace(item);

        _mockDataCollection.Verify(x => x.Get(), Times.Never);
    }

    [Fact]
    public async Task Replace_ShouldUpdateItemInCache()
    {
        var item = new DomainTestModel { Name = "1" };
        _mockDataCollection.Setup(x => x.Get()).Returns(() => new List<DomainTestModel> { item });
        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);

        // Initialize cache
        _testCachedContext.Get().Should().HaveCount(1);

        var updatedItem = new DomainTestModel { Id = item.Id, Name = "Updated" };
        await _testCachedContext.Replace(updatedItem);

        _testCachedContext.Get().Should().HaveCount(1);
        _testCachedContext.Get().First().Name.Should().Be("Updated");
    }

    [Fact]
    public async Task Update_ById_ShouldReFetchSingleItem()
    {
        var item = new DomainTestModel { Name = "1" };
        var updatedItem = new DomainTestModel { Id = item.Id, Name = "2" };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);
        _mockDataCollection.Setup(x => x.GetSingle(item.Id)).Returns(updatedItem);

        await _testCachedContext.Update(item.Id, x => x.Name, "2");

        _mockDataCollection.Verify(x => x.Get(), Times.Never);
        _mockDataCollection.Verify(x => x.GetSingle(item.Id), Times.Once);
    }

    [Fact]
    public async Task Update_ById_UpdateDefinition_ShouldReFetchSingleItem()
    {
        var item = new DomainTestModel { Name = "1" };
        var updatedItem = new DomainTestModel { Id = item.Id, Name = "2" };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);
        _mockDataCollection.Setup(x => x.GetSingle(item.Id)).Returns(updatedItem);

        await _testCachedContext.Update(item.Id, Builders<DomainTestModel>.Update.Set(x => x.Name, "2"));

        _mockDataCollection.Verify(x => x.Get(), Times.Never);
        _mockDataCollection.Verify(x => x.GetSingle(item.Id), Times.Once);
    }

    [Fact]
    public async Task Update_ById_ShouldUpdateItemInCache()
    {
        var item = new DomainTestModel { Name = "1" };
        var updatedItem = new DomainTestModel { Id = item.Id, Name = "Updated" };

        _mockDataCollection.Setup(x => x.Get()).Returns(() => new List<DomainTestModel> { item });
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);
        _mockDataCollection.Setup(x => x.GetSingle(item.Id)).Returns(updatedItem);

        // Initialize cache
        _testCachedContext.Get().First().Name.Should().Be("1");

        await _testCachedContext.Update(item.Id, x => x.Name, "Updated");

        _testCachedContext.Get().First().Name.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateMany_ShouldFullRefresh()
    {
        _mockDataCollection.Setup(x => x.UpdateManyAsync(It.IsAny<Expression<Func<DomainTestModel, bool>>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);

        await _testCachedContext.UpdateMany(x => x.Name == "1", Builders<DomainTestModel>.Update.Set(x => x.Name, "3"));

        // Once for Get(predicate) to collect IDs, once for Refresh after update
        _mockDataCollection.Verify(x => x.Get(), Times.Exactly(2));
    }

    [Fact]
    public async Task Update_ByFilterExpression_ShouldFullRefresh()
    {
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<DomainTestModel>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);

        await _testCachedContext.Update(x => x.Name == "1", Builders<DomainTestModel>.Update.Set(x => x.Name, "Updated"));

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task Update_ByFilterExpression_ShouldNotThrow_WhenFilterNoLongerMatchesAfterUpdate()
    {
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<FilterDefinition<DomainTestModel>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);

        var act = () => _testCachedContext.Update(x => x.Name == "NonExistent", Builders<DomainTestModel>.Update.Set(x => x.Name, "Updated"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FindAndUpdate_ShouldFullRefresh()
    {
        _mockDataCollection.Setup(x => x.FindAndUpdateAsync(It.IsAny<FilterDefinition<DomainTestModel>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);

        await _testCachedContext.FindAndUpdate(x => x.Name == "1", Builders<DomainTestModel>.Update.Set(x => x.Name, "Updated"));

        _mockDataCollection.Verify(x => x.Get(), Times.Once);
    }

    [Fact]
    public async Task FindAndUpdate_ShouldNotThrow_WhenFilterNoLongerMatchesAfterUpdate()
    {
        _mockDataCollection.Setup(x => x.FindAndUpdateAsync(It.IsAny<FilterDefinition<DomainTestModel>>(), It.IsAny<UpdateDefinition<DomainTestModel>>()))
                           .Returns(Task.CompletedTask);

        var act = () => _testCachedContext.FindAndUpdate(x => x.Name == "NonExistent", Builders<DomainTestModel>.Update.Set(x => x.Name, "Updated"));

        await act.Should().NotThrowAsync();
    }
}

public class CachedContextOrderingTests
{
    private readonly Mock<Core.Context.Base.IMongoCollection<DomainTestModel>> _mockDataCollection;
    private readonly TestOrderedCachedContext _testOrderedContext;

    public CachedContextOrderingTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new Mock<Core.Context.Base.IMongoCollection<DomainTestModel>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainTestModel>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        _mockDataCollection.Setup(x => x.Get()).Returns(() => new List<DomainTestModel>());
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _testOrderedContext = new TestOrderedCachedContext(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object, "test");
    }

    [Fact]
    public async Task Update_ById_ShouldReorderCache_WhenOrderingFieldChanges()
    {
        var itemA = new DomainTestModel { Name = "A" };
        var itemB = new DomainTestModel { Name = "B" };
        var itemC = new DomainTestModel { Name = "C" };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(() => new List<DomainTestModel>
            {
                itemA,
                itemB,
                itemC
            }
        );
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);

        // Initialize cache — should be ordered A, B, C
        _testOrderedContext.Get().Select(x => x.Name).Should().ContainInOrder("A", "B", "C");

        // Update A's name to Z — should reorder to B, C, Z
        var updatedItem = new DomainTestModel { Id = itemA.Id, Name = "Z" };
        _mockDataCollection.Setup(x => x.GetSingle(itemA.Id)).Returns(updatedItem);

        await _testOrderedContext.Update(itemA.Id, x => x.Name, "Z");

        _testOrderedContext.Get().Select(x => x.Name).Should().ContainInOrder("B", "C", "Z");
    }

    [Fact]
    public async Task Update_ById_UpdateDefinition_ShouldReorderCache_WhenOrderingFieldChanges()
    {
        var itemA = new DomainTestModel { Name = "A" };
        var itemB = new DomainTestModel { Name = "B" };
        var itemC = new DomainTestModel { Name = "C" };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(() => new List<DomainTestModel>
            {
                itemA,
                itemB,
                itemC
            }
        );
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainTestModel>>())).Returns(Task.CompletedTask);

        // Initialize cache
        _testOrderedContext.Get().Select(x => x.Name).Should().ContainInOrder("A", "B", "C");

        // Update A's name to Z — should reorder to B, C, Z
        var updatedItem = new DomainTestModel { Id = itemA.Id, Name = "Z" };
        _mockDataCollection.Setup(x => x.GetSingle(itemA.Id)).Returns(updatedItem);

        await _testOrderedContext.Update(itemA.Id, Builders<DomainTestModel>.Update.Set(x => x.Name, "Z"));

        _testOrderedContext.Get().Select(x => x.Name).Should().ContainInOrder("B", "C", "Z");
    }

    [Fact]
    public async Task Replace_ShouldReorderCache_WhenOrderingFieldChanges()
    {
        var itemA = new DomainTestModel { Name = "A" };
        var itemB = new DomainTestModel { Name = "B" };
        var itemC = new DomainTestModel { Name = "C" };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(() => new List<DomainTestModel>
            {
                itemA,
                itemB,
                itemC
            }
        );
        _mockDataCollection.Setup(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<DomainTestModel>())).Returns(Task.CompletedTask);

        // Initialize cache
        _testOrderedContext.Get().Select(x => x.Name).Should().ContainInOrder("A", "B", "C");

        // Replace A with Z — should reorder to B, C, Z
        var replacedItem = new DomainTestModel { Id = itemA.Id, Name = "Z" };
        await _testOrderedContext.Replace(replacedItem);

        _testOrderedContext.Get().Select(x => x.Name).Should().ContainInOrder("B", "C", "Z");
    }
}
