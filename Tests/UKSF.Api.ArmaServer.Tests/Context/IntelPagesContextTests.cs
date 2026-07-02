using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Context;

public class IntelPagesContextTests
{
    private readonly Mock<IMongoCollection<DomainIntelPage>> _mockDataCollection = new();
    private readonly IntelPagesContext _intelPagesContext;

    public IntelPagesContextTests()
    {
        Mock<IMongoCollectionFactory> mockFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        mockFactory.Setup(x => x.CreateMongoCollection<DomainIntelPage>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _intelPagesContext = new IntelPagesContext(mockFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_return_intel_pages_from_collection()
    {
        DomainIntelPage a = new() { Title = "Enemy Forces" };
        DomainIntelPage b = new() { Title = "Area of Operations" };
        _mockDataCollection.Setup(x => x.Get()).Returns([a, b]);

        var subject = _intelPagesContext.Get();

        subject.Should().Contain([a, b]);
    }
}
