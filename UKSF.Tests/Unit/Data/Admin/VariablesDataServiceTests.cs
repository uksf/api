using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Data.Admin;

public class VariablesDataServiceTests
{
    private readonly VariablesContext _variablesContext;
    private List<DomainVariableItem> _mockCollection;

    public VariablesDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        Mock<IMongoCollection<DomainVariableItem>> mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainVariableItem>(It.IsAny<string>())).Returns(mockDataCollection.Object);
        mockDataCollection.Setup(x => x.Get()).Returns(() => _mockCollection);

        _variablesContext = new VariablesContext(mockDataCollectionFactory.Object, mockEventBus.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        DomainVariableItem item1 = new() { Key = "MISSIONS_PATH" };
        DomainVariableItem item2 = new() { Key = "SERVER_PATH" };
        DomainVariableItem item3 = new() { Key = "DISCORD_IDS" };
        _mockCollection =
        [
            item1,
            item2,
            item3
        ];

        var subject = _variablesContext.Get();

        subject.Should().ContainInOrder(item3, item1, item2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("game path")]
    public void ShouldGetNothingWhenNoKeyOrNotFound(string key)
    {
        DomainVariableItem item1 = new() { Key = "MISSIONS_PATH" };
        DomainVariableItem item2 = new() { Key = "SERVER_PATH" };
        DomainVariableItem item3 = new() { Key = "DISCORD_IDS" };
        _mockCollection =
        [
            item1,
            item2,
            item3
        ];

        var subject = _variablesContext.GetSingle(key);

        subject.Should().Be(null);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ShouldThrowForUpdateWhenNoKeyOrNull(string key)
    {
        _mockCollection = [];

        var act = async () => await _variablesContext.Update(key, "75");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ShouldThrowForDeleteWhenNoKeyOrNull(string key)
    {
        _mockCollection = [];

        var act = async () => await _variablesContext.Delete(key);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
