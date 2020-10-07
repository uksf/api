using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class DataCollectionFactoryTests {
        [Fact]
        public void ShouldCreateDataCollection() {
            Mock<IMongoDatabase> mockMongoDatabase = new Mock<IMongoDatabase>();

            DataCollectionFactory dataCollectionFactory = new DataCollectionFactory(mockMongoDatabase.Object);

            IDataCollection<TestDataModel> subject = dataCollectionFactory.CreateDataCollection<TestDataModel>("test");

            subject.Should().NotBeNull();
        }
    }
}
