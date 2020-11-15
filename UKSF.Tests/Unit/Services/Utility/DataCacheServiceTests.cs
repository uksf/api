using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Context;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class DataCacheServiceTests {
        [Fact]
        public void When_refreshing_data_caches() {
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();
            Mock<IRanksDataService> mockRanksDataService = new Mock<IRanksDataService>();
            Mock<IRolesDataService> mockRolesDataService = new Mock<IRolesDataService>();

            IServiceProvider serviceProvider = new ServiceCollection().AddSingleton(_ => mockAccountDataService.Object)
                                                                      .AddSingleton(_ => mockRanksDataService.Object)
                                                                      .AddSingleton(_ => mockRolesDataService.Object)
                                                                      .BuildServiceProvider();
            DataCacheService dataCacheService = new DataCacheService(serviceProvider);

            dataCacheService.RefreshCachedData();

            mockAccountDataService.Verify(x => x.Refresh(), Times.Once);
            mockRanksDataService.Verify(x => x.Refresh(), Times.Once);
            mockRolesDataService.Verify(x => x.Refresh(), Times.Once);
        }
    }
}
