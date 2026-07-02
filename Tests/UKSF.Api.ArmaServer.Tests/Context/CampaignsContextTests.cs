using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Context;

public class CampaignsContextTests
{
    private readonly Mock<IMongoCollection<DomainCampaign>> _mockDataCollection = new();
    private readonly CampaignsContext _campaignsContext;

    public CampaignsContextTests()
    {
        Mock<IMongoCollectionFactory> mockFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        mockFactory.Setup(x => x.CreateMongoCollection<DomainCampaign>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _campaignsContext = new CampaignsContext(mockFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_return_campaigns_from_collection()
    {
        DomainCampaign a = new() { Name = "Op Storm" };
        DomainCampaign b = new() { Name = "Op Thunder" };
        _mockDataCollection.Setup(x => x.Get()).Returns([a, b]);

        var subject = _campaignsContext.Get();

        subject.Should().Contain([a, b]);
    }
}
