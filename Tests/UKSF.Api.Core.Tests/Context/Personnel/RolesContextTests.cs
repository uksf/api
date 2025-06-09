using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Personnel;

public class RolesContextTests
{
    private readonly Mock<IMongoCollection<DomainRole>> _mockDataCollection;
    private readonly RolesContext _rolesContext;

    public RolesContextTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new Mock<IMongoCollection<DomainRole>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainRole>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _rolesContext = new RolesContext(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        DomainRole role1 = new() { Name = "Rifleman" };
        DomainRole role2 = new() { Name = "Trainee" };
        DomainRole role3 = new() { Name = "Marksman" };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<DomainRole>
            {
                role1,
                role2,
                role3
            }
        );

        var subject = _rolesContext.Get();

        subject.Should().ContainInOrder(role3, role1, role2);
    }

    [Fact]
    public void ShouldGetSingleByName()
    {
        DomainRole role1 = new() { Name = "Rifleman" };
        DomainRole role2 = new() { Name = "Trainee" };
        DomainRole role3 = new() { Name = "Marksman" };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<DomainRole>
            {
                role1,
                role2,
                role3
            }
        );

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
