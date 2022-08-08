using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel;

public class RolesDataServiceTests
{
    private readonly Mock<IMongoCollection<DomainRole>> _mockDataCollection;
    private readonly RolesContext _rolesContext;

    public RolesDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainRole>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

        _rolesContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        DomainRole role1 = new() { Name = "Rifleman" };
        DomainRole role2 = new() { Name = "Trainee" };
        DomainRole role3 = new() { Name = "Marksman" };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainRole> { role1, role2, role3 });

        var subject = _rolesContext.Get();

        subject.Should().ContainInOrder(role3, role1, role2);
    }

    [Fact]
    public void ShouldGetSingleByName()
    {
        DomainRole role1 = new() { Name = "Rifleman" };
        DomainRole role2 = new() { Name = "Trainee" };
        DomainRole role3 = new() { Name = "Marksman" };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainRole> { role1, role2, role3 });

        var subject = _rolesContext.GetSingle("Trainee");

        subject.Should().Be(role2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ShouldGetNothingWhenNoName(string name)
    {
        _mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainRole>());

        var subject = _rolesContext.GetSingle(name);

        subject.Should().Be(null);
    }
}
