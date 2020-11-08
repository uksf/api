using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel {
    public class RolesDataServiceTests {
        private readonly Mock<IDataCollection<Role>> mockDataCollection;
        private readonly RolesDataService rolesDataService;

        public RolesDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<Role>> mockDataEventBus = new Mock<IDataEventBus<Role>>();
            mockDataCollection = new Mock<IDataCollection<Role>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Role>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            rolesDataService = new RolesDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldGetNothingWhenNoName(string name) {
            mockDataCollection.Setup(x => x.Get()).Returns(new List<Role>());

            Role subject = rolesDataService.GetSingle(name);

            subject.Should().Be(null);
        }

        [Fact]
        public void ShouldGetSingleByName() {
            Role role1 = new Role { name = "Rifleman" };
            Role role2 = new Role { name = "Trainee" };
            Role role3 = new Role { name = "Marksman" };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Role> { role1, role2, role3 });

            Role subject = rolesDataService.GetSingle("Trainee");

            subject.Should().Be(role2);
        }

        [Fact]
        public void Should_get_collection_in_order() {
            Role role1 = new Role { name = "Rifleman" };
            Role role2 = new Role { name = "Trainee" };
            Role role3 = new Role { name = "Marksman" };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Role> { role1, role2, role3 });

            IEnumerable<Role> subject = rolesDataService.Get();

            subject.Should().ContainInOrder(role3, role1, role2);
        }
    }
}
