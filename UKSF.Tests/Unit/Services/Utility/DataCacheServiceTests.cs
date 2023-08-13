using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility;

public class DataCacheServiceTests
{
    [Fact]
    public void When_refreshing_data_caches()
    {
        Mock<IAccountContext> mockAccountDataService = new();
        Mock<IRanksContext> mockRanksDataService = new();
        Mock<IRolesContext> mockRolesDataService = new();

        var serviceProvider = new ServiceCollection().AddSingleton<ICachedMongoContext>(_ => mockAccountDataService.Object)
                                                     .AddSingleton<ICachedMongoContext>(_ => mockRanksDataService.Object)
                                                     .AddSingleton<ICachedMongoContext>(_ => mockRolesDataService.Object)
                                                     .BuildServiceProvider();
        DataCacheService dataCacheService = new(serviceProvider);

        dataCacheService.RefreshCachedData();

        mockAccountDataService.Verify(x => x.Refresh(), Times.Once);
        mockRanksDataService.Verify(x => x.Refresh(), Times.Once);
        mockRolesDataService.Verify(x => x.Refresh(), Times.Once);
    }
}
