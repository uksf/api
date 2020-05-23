using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Game;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Game;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Data.Game {
    public class GameServersDataServiceTests {
        private readonly Mock<IDataCollection<GameServer>> mockDataCollection;
        private readonly GameServersDataService gameServersDataService;

        public GameServersDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IGameServersDataService>> mockDataEventBus = new Mock<IDataEventBus<IGameServersDataService>>();
            mockDataCollection = new Mock<IDataCollection<GameServer>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<GameServer>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            gameServersDataService = new GameServersDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void ShouldGetSortedCollection() {
            GameServer rank1 = new GameServer {order = 2};
            GameServer rank2 = new GameServer {order = 0};
            GameServer rank3 = new GameServer {order = 1};

            mockDataCollection.Setup(x => x.Get()).Returns(new List<GameServer> {rank1, rank2, rank3});

            List<GameServer> subject = gameServersDataService.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }
    }
}
