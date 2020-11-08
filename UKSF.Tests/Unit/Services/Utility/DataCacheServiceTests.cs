using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Services.Data;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class DataCacheServiceTests {
        [Fact]
        public void ShouldCallDataServiceRefresh() {
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();
            Mock<IRanksDataService> mockRanksDataService = new Mock<IRanksDataService>();
            Mock<IRolesDataService> mockRolesDataService = new Mock<IRolesDataService>();

            mockAccountDataService.Setup(x => x.Refresh());
            mockRanksDataService.Setup(x => x.Refresh());
            mockRolesDataService.Setup(x => x.Refresh());

            IServiceProvider serviceProvider = new ServiceCollection().AddTransient(_ => mockAccountDataService.Object)
                                                                      .AddTransient(_ => mockRanksDataService.Object)
                                                                      .AddTransient(_ => mockRolesDataService.Object)
                                                                      .BuildServiceProvider();
            DataCacheService dataCacheService = new DataCacheService(serviceProvider);

            dataCacheService.InvalidateCachedData();

            mockAccountDataService.Verify(x => x.Refresh(), Times.Once);
            mockRanksDataService.Verify(x => x.Refresh(), Times.Once);
            mockRolesDataService.Verify(x => x.Refresh(), Times.Once);
        }
    }
}
