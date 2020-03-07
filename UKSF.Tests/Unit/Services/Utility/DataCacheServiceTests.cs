using System.Collections.Generic;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Services.Utility {
    public class DataCacheServiceTests {
        [Fact]
        public void ShouldCallDataServiceRefresh() {
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();
            Mock<IRanksDataService> mockRanksDataService = new Mock<IRanksDataService>();
            Mock<IRolesDataService> mockRolesDataService = new Mock<IRolesDataService>();

            mockAccountDataService.Setup(x => x.Refresh());
            mockRanksDataService.Setup(x => x.Refresh());
            mockRolesDataService.Setup(x => x.Refresh());

            DataCacheService dataCacheService = new DataCacheService();

            dataCacheService.RegisterCachedDataServices(new HashSet<ICachedDataService> {mockAccountDataService.Object, mockRanksDataService.Object, mockRolesDataService.Object});
            dataCacheService.InvalidateCachedData();

            mockAccountDataService.Verify(x => x.Refresh(), Times.Once);
            mockRanksDataService.Verify(x => x.Refresh(), Times.Once);
            mockRolesDataService.Verify(x => x.Refresh(), Times.Once);
        }
    }
}
