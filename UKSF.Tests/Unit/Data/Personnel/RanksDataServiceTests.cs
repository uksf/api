using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Data.Personnel {
    public class RanksDataServiceTests {
        private readonly Mock<IDataCollection<Rank>> mockDataCollection;
        private readonly RanksDataService ranksDataService;

        public RanksDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IRanksDataService>> mockDataEventBus = new Mock<IDataEventBus<IRanksDataService>>();
            mockDataCollection = new Mock<IDataCollection<Rank>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Rank>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            ranksDataService = new RanksDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldGetNothingWhenNoNameOrNull(string name) {
            mockDataCollection.Setup(x => x.Get()).Returns(new List<Rank>());

            Rank subject = ranksDataService.GetSingle(name);

            subject.Should().Be(null);
        }

        [Fact]
        public void ShouldGetSingleByName() {
            Rank rank1 = new Rank { name = "Private", order = 2 };
            Rank rank2 = new Rank { name = "Recruit", order = 1 };
            Rank rank3 = new Rank { name = "Candidate", order = 0 };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Rank> { rank1, rank2, rank3 });

            Rank subject = ranksDataService.GetSingle("Recruit");

            subject.Should().Be(rank2);
        }

        [Fact]
        public void ShouldGetSortedCollection() {
            Rank rank1 = new Rank { order = 2 };
            Rank rank2 = new Rank { order = 0 };
            Rank rank3 = new Rank { order = 1 };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Rank> { rank1, rank2, rank3 });

            IEnumerable<Rank> subject = ranksDataService.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }
    }
}
