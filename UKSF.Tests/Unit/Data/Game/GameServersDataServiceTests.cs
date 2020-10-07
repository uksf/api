using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Game;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Game;
using Xunit;

namespace UKSF.Tests.Unit.Data.Game {
    public class GameServersDataServiceTests {
        private readonly GameServersDataService gameServersDataService;
        private readonly Mock<IDataCollection<GameServer>> mockDataCollection;

        public GameServersDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<GameServer>> mockDataEventBus = new Mock<IDataEventBus<GameServer>>();
            mockDataCollection = new Mock<IDataCollection<GameServer>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<GameServer>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            gameServersDataService = new GameServersDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order() {
            GameServer rank1 = new GameServer { order = 2 };
            GameServer rank2 = new GameServer { order = 0 };
            GameServer rank3 = new GameServer { order = 1 };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<GameServer> { rank1, rank2, rank3 });

            IEnumerable<GameServer> subject = gameServersDataService.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }
    }
}
