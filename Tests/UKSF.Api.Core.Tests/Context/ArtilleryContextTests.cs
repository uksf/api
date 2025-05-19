using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Context;

public class ArtilleryContextTests
{
    private readonly Mock<IMongoCollection<DomainArtillery>> _mockCollection = new();
    private readonly ArtilleryContext _sut;

    public ArtilleryContextTests()
    {
        var mockMongoCollectionFactory = new Mock<IMongoCollectionFactory>();
        var mockEventBus = new Mock<IEventBus>();
        var mockVariablesService = new Mock<IVariablesService>();

        mockMongoCollectionFactory.Setup(x => x.CreateMongoCollection<DomainArtillery>(It.IsAny<string>())).Returns(_mockCollection.Object);

        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _sut = new ArtilleryContext(mockMongoCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    public class GetCollection : ArtilleryContextTests
    {
        [Fact]
        public void Should_ReturnCollectionOrderedByKey()
        {
            // Arrange
            var unorderedData = new List<DomainArtillery>
            {
                new() { Key = "z-key" },
                new() { Key = "a-key" },
                new() { Key = "m-key" }
            };

            _mockCollection.Setup(x => x.Get(It.IsAny<Func<DomainArtillery, bool>>())).Returns(unorderedData);

            // Act
            var result = _sut.Get().ToList();

            // Assert
            result.Should().BeInAscendingOrder(x => x.Key);
        }
    }

    public class GetSingleMethod : ArtilleryContextTests
    {
        [Fact]
        public void Should_ReturnMatchingArtillery_When_SearchingById()
        {
            // Arrange
            const string targetId = "test-id";
            var expectedArtillery = new DomainArtillery { Id = targetId, Key = "test-key" };

            _mockCollection.Setup(x => x.Get()).Returns([expectedArtillery]);

            // Act
            var result = _sut.GetSingle(targetId);

            // Assert
            result.Should().BeEquivalentTo(expectedArtillery);
        }

        [Fact]
        public void Should_ReturnMatchingArtillery_When_SearchingByKey()
        {
            // Arrange
            const string targetKey = "test-key";
            var expectedArtillery = new DomainArtillery { Id = "test-id", Key = targetKey };

            _mockCollection.Setup(x => x.Get()).Returns([expectedArtillery]);

            // Act
            var result = _sut.GetSingle(targetKey);

            // Assert
            result.Should().BeEquivalentTo(expectedArtillery);
        }
    }
}
