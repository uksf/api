using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Data.Game;

public class GameServersContextTests
{
    private readonly GameServersContext _gameServersContext;
    private readonly Mock<IMongoCollection<GameServer>> _mockDataCollection;

    public GameServersContextTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<GameServer>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _gameServersContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        GameServer rank1 = new() { Order = 2 };
        GameServer rank2 = new() { Order = 0 };
        GameServer rank3 = new() { Order = 1 };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<GameServer> { rank1, rank2, rank3 });

        var subject = _gameServersContext.Get();

        subject.Should().ContainInOrder(rank2, rank3, rank1);
    }
}
