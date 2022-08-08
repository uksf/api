using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data.Admin;

public class VariablesDataServiceTests
{
    private readonly Mock<Api.Base.Context.IMongoCollection<VariableItem>> _mockDataCollection;
    private readonly VariablesContext _variablesContext;
    private List<VariableItem> _mockCollection;

    public VariablesDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<VariableItem>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        _mockDataCollection.Setup(x => x.Get()).Returns(() => _mockCollection);

        _variablesContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        VariableItem item1 = new() { Key = "MISSIONS_PATH" };
        VariableItem item2 = new() { Key = "SERVER_PATH" };
        VariableItem item3 = new() { Key = "DISCORD_IDS" };
        _mockCollection = new() { item1, item2, item3 };

        var subject = _variablesContext.Get();

        subject.Should().ContainInOrder(item3, item1, item2);
    }

    [Fact]
    public async Task ShouldDeleteItem()
    {
        VariableItem item1 = new() { Key = "DISCORD_ID", Item = "50" };
        _mockCollection = new() { item1 };

        _mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id) => _mockCollection.RemoveAll(x => x.Id == id));

        await _variablesContext.Delete("discord id");

        _mockCollection.Should().HaveCount(0);
    }

    [Fact]
    public void ShouldGetItemByKey()
    {
        VariableItem item1 = new() { Key = "MISSIONS_PATH" };
        VariableItem item2 = new() { Key = "SERVER_PATH" };
        VariableItem item3 = new() { Key = "DISCORD_IDS" };
        _mockCollection = new() { item1, item2, item3 };

        var subject = _variablesContext.GetSingle("server path");

        subject.Should().Be(item2);
    }

    [Fact]
    public async Task ShouldUpdateItemValue()
    {
        VariableItem subject = new() { Key = "DISCORD_ID", Item = "50" };
        _mockCollection = new() { subject };

        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<VariableItem>>()))
                           .Returns(Task.CompletedTask)
                           .Callback((string id, UpdateDefinition<VariableItem> _) => _mockCollection.First(x => x.Id == id).Item = "75");

        await _variablesContext.Update("discord id", "75");

        subject.Item.Should().Be("75");
    }

    [Theory]
    [InlineData("")]
    [InlineData("game path")]
    public void ShouldGetNothingWhenNoKeyOrNotFound(string key)
    {
        VariableItem item1 = new() { Key = "MISSIONS_PATH" };
        VariableItem item2 = new() { Key = "SERVER_PATH" };
        VariableItem item3 = new() { Key = "DISCORD_IDS" };
        _mockCollection = new() { item1, item2, item3 };

        var subject = _variablesContext.GetSingle(key);

        subject.Should().Be(null);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ShouldThrowForUpdateWhenNoKeyOrNull(string key)
    {
        _mockCollection = new();

        var act = async () => await _variablesContext.Update(key, "75");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ShouldThrowForDeleteWhenNoKeyOrNull(string key)
    {
        _mockCollection = new();

        var act = async () => await _variablesContext.Delete(key);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
