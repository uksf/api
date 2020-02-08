using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel {
    public class RanksDataServiceTests {
        private readonly Mock<IDataCollection> mockDataCollection;
        private readonly RanksDataService ranksDataService;

        public RanksDataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            Mock<IDataEventBus<IRanksDataService>> mockDataEventBus = new Mock<IDataEventBus<IRanksDataService>>();

            ranksDataService = new RanksDataService(mockDataCollection.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void ShouldGetSortedCollection() {
            Rank rank1 = new Rank {name = "Private", order = 2};
            Rank rank2 = new Rank {name = "Recruit", order = 1};
            Rank rank3 = new Rank {name = "Candidate", order = 0};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2, rank3};

            mockDataCollection.Setup(x => x.Get<Rank>()).Returns(mockCollection);

            List<Rank> subject = ranksDataService.Get();

            subject.Should().ContainInOrder(rank3, rank2, rank1);
        }

        [Fact]
        public void ShouldGetSingleByName() {
            Rank rank1 = new Rank {name = "Private", order = 2};
            Rank rank2 = new Rank {name = "Recruit", order = 1};
            Rank rank3 = new Rank {name = "Candidate", order = 0};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2, rank3};

            mockDataCollection.Setup(x => x.Get<Rank>()).Returns(mockCollection);

            Rank subject = ranksDataService.GetSingle("Recruit");

            subject.Should().Be(rank2);
        }
    }
}
