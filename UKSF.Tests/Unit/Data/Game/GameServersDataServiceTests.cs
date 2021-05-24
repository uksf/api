using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data.Game
{
    public class GameServersDataServiceTests
    {
        private readonly GameServersContext _gameServersContext;
        private readonly Mock<IMongoCollection<GameServer>> _mockDataCollection;

        public GameServersDataServiceTests()
        {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IEventBus> mockEventBus = new();
            _mockDataCollection = new();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<GameServer>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

            _gameServersContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order()
        {
            GameServer rank1 = new() { Order = 2 };
            GameServer rank2 = new() { Order = 0 };
            GameServer rank3 = new() { Order = 1 };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<GameServer> { rank1, rank2, rank3 });

            IEnumerable<GameServer> subject = _gameServersContext.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }
    }
}
