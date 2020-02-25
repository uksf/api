using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Data.Personnel {
    public class RolesDataServiceTests {
        private readonly Mock<IDataCollection<Role>> mockDataCollection;
        private readonly RolesDataService rolesDataService;

        public RolesDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IRolesDataService>> mockDataEventBus = new Mock<IDataEventBus<IRolesDataService>>();
            mockDataCollection = new Mock<IDataCollection<Role>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Role>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            rolesDataService = new RolesDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void ShouldGetSortedCollection() {
            Role role1 = new Role {name = "Rifleman"};
            Role role2 = new Role {name = "Trainee"};
            Role role3 = new Role {name = "Marksman"};

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Role> {role1, role2, role3});

            List<Role> subject = rolesDataService.Get();

            subject.Should().ContainInOrder(role3, role1, role2);
        }

        [Fact]
        public void ShouldGetSingleByName() {
            Role role1 = new Role {name = "Rifleman"};
            Role role2 = new Role {name = "Trainee"};
            Role role3 = new Role {name = "Marksman"};

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Role> {role1, role2, role3});

            Role subject = rolesDataService.GetSingle("Trainee");

            subject.Should().Be(role2);
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldGetNothingWhenNoName(string name) {
            mockDataCollection.Setup(x => x.Get()).Returns(new List<Role>());

            Role subject = rolesDataService.GetSingle(name);

            subject.Should().Be(null);
        }
    }
}
