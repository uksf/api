using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Data.Admin;

public class VariablesDataServiceTests
{
    private readonly VariablesContext _variablesContext;
    private List<VariableItem> _mockCollection;

    public VariablesDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        Mock<IMongoCollection<VariableItem>> mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<VariableItem>(It.IsAny<string>())).Returns(mockDataCollection.Object);
        mockDataCollection.Setup(x => x.Get()).Returns(() => _mockCollection);

        _variablesContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        VariableItem item1 = new() { Key = "MISSIONS_PATH" };
        VariableItem item2 = new() { Key = "SERVER_PATH" };
        VariableItem item3 = new() { Key = "DISCORD_IDS" };
        _mockCollection = new()
        {
            item1,
            item2,
            item3
        };

        var subject = _variablesContext.Get();

        subject.Should().ContainInOrder(item3, item1, item2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("game path")]
    public void ShouldGetNothingWhenNoKeyOrNotFound(string key)
    {
        VariableItem item1 = new() { Key = "MISSIONS_PATH" };
        VariableItem item2 = new() { Key = "SERVER_PATH" };
        VariableItem item3 = new() { Key = "DISCORD_IDS" };
        _mockCollection = new()
        {
            item1,
            item2,
            item3
        };

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
