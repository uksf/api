using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Models.Request;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class ArtilleryControllerTests
{
    private readonly Mock<IArtilleryContext> _mockArtilleryContext = new();
    private readonly ArtilleryController _sut;

    public ArtilleryControllerTests()
    {
        _sut = new ArtilleryController(_mockArtilleryContext.Object);
    }

    [Fact]
    public async Task Get_Should_ReturnArtillery_When_ArtilleryExists()
    {
        // Arrange
        const string Key = "test-key";
        var expectedArtillery = new DomainArtillery { Key = Key, Data = "test-data" };

        _mockArtilleryContext.Setup(x => x.GetSingle(Key)).Returns(expectedArtillery);

        // Act
        var result = await Task.FromResult(_sut.Get(Key));

        // Assert
        result.Should().BeOfType<ActionResult<DomainArtillery>>();
        result.Value.Should().BeEquivalentTo(expectedArtillery);
        _mockArtilleryContext.Verify(x => x.GetSingle(Key), Times.Once);
    }

    [Fact]
    public async Task Get_Should_ReturnNewArtillery_When_ArtilleryDoesNotExist()
    {
        // Arrange
        const string Key = "test-key";

        _mockArtilleryContext.Setup(x => x.GetSingle(Key)).Returns((DomainArtillery)null);

        // Act
        var result = await Task.FromResult(_sut.Get(Key));

        // Assert
        result.Should().BeOfType<ActionResult<DomainArtillery>>();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().NotBeNull();
        result.Value.Key.Should().Be(Key);
        result.Value.Data.Should().Be("{}");
        _mockArtilleryContext.Verify(x => x.GetSingle(Key), Times.Once);
    }

    [Fact]
    public async Task Put_Should_CreateNewArtillery_When_ArtilleryDoesNotExist()
    {
        // Arrange
        const string Key = "test-key";
        const string Data = "test-data";

        _mockArtilleryContext.Setup(x => x.GetSingle(Key)).Returns((DomainArtillery)null);

        // Act
        var result = await _sut.Put(Key, new UpdateArtilleryRequest { Data = Data });

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockArtilleryContext.Verify(x => x.GetSingle(Key), Times.Once);
        _mockArtilleryContext.Verify(x => x.Add(It.Is<DomainArtillery>(a => a.Key == Key && a.Data == Data)), Times.Once);
    }

    [Fact]
    public async Task Put_Should_UpdateArtillery_When_ArtilleryExists()
    {
        // Arrange
        const string Key = "test-key";
        const string Data = "test-data";
        var existingArtillery = new DomainArtillery { Key = Key, Data = "old-data" };

        _mockArtilleryContext.Setup(x => x.GetSingle(Key)).Returns(existingArtillery);

        // Act
        var result = await _sut.Put(Key, new UpdateArtilleryRequest { Data = Data });

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockArtilleryContext.Verify(x => x.GetSingle(Key), Times.Once);
        _mockArtilleryContext.Verify(x => x.Update(existingArtillery.Id, It.IsAny<Expression<Func<DomainArtillery, string>>>(), Data), Times.Once);
    }
}
