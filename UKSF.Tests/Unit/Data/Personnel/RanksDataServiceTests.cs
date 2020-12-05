using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel {
    public class RanksDataServiceTests {
        private readonly Mock<IMongoCollection<Rank>> _mockDataCollection;
        private readonly RanksContext _ranksContext;

        public RanksDataServiceTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IEventBus> mockEventBus = new();
            _mockDataCollection = new Mock<IMongoCollection<Rank>>();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Rank>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

            _ranksContext = new RanksContext(mockDataCollectionFactory.Object, mockEventBus.Object);
        }

        [Theory, InlineData(""), InlineData(null)]
        public void Should_return_nothing_for_empty_or_null_name(string name) {
            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Rank>());

            Rank subject = _ranksContext.GetSingle(name);

            subject.Should().Be(null);
        }

        [Fact]
        public void Should_return_collection_in_order() {
            Rank rank1 = new() { Order = 2 };
            Rank rank2 = new() { Order = 0 };
            Rank rank3 = new() { Order = 1 };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Rank> { rank1, rank2, rank3 });

            IEnumerable<Rank> subject = _ranksContext.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }

        [Fact]
        public void Should_return_item_by_name() {
            Rank rank1 = new() { Name = "Private", Order = 2 };
            Rank rank2 = new() { Name = "Recruit", Order = 1 };
            Rank rank3 = new() { Name = "Candidate", Order = 0 };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Rank> { rank1, rank2, rank3 });

            Rank subject = _ranksContext.GetSingle("Recruit");

            subject.Should().Be(rank2);
        }
    }
}
