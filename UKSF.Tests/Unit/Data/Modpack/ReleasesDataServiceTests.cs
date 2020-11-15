using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.Data;
using UKSF.Api.Shared.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data.Modpack {
    public class ReleasesDataServiceTests {
        private readonly ReleasesDataService releasesDataService;
        private readonly Mock<IDataCollection<ModpackRelease>> mockDataCollection;

        public ReleasesDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<ModpackRelease>> mockDataEventBus = new Mock<IDataEventBus<ModpackRelease>>();
            mockDataCollection = new Mock<IDataCollection<ModpackRelease>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<ModpackRelease>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            releasesDataService = new ReleasesDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order() {
            ModpackRelease item1 = new ModpackRelease { Version = "4.19.11" };
            ModpackRelease item2 = new ModpackRelease { Version = "5.19.6" };
            ModpackRelease item3 = new ModpackRelease { Version = "5.18.8" };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackRelease> { item1, item2, item3 });

            IEnumerable<ModpackRelease> subject = releasesDataService.Get();

            subject.Should().ContainInOrder(item2, item3, item1);
        }
    }
}
