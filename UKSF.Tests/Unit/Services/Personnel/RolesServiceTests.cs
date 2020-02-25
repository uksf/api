using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Services.Personnel {
    public class RolesServiceTests {
        private readonly Mock<IRolesDataService> mockRolesDataService;
        private readonly RolesService rolesService;

        public RolesServiceTests() {
            mockRolesDataService = new Mock<IRolesDataService>();
            rolesService = new RolesService(mockRolesDataService.Object);
        }

        [Theory, InlineData("Trainee", "Rifleman", 1), InlineData("Rifleman", "Trainee", -1), InlineData("Rifleman", "Rifleman", 0)]
        public void ShouldGetCorrectSortValueByName(string nameA, string nameB, int expected) {
            Role role1 = new Role {name = "Rifleman", order = 0};
            Role role2 = new Role {name = "Trainee", order = 1};
            List<Role> mockCollection = new List<Role> {role1, role2};

            mockRolesDataService.Setup(x => x.Get()).Returns(mockCollection);
            mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.name == x));

            int subject = rolesService.Sort(nameA, nameB);

            subject.Should().Be(expected);
        }

        [Fact]
        public void ShouldReturnZeroForSortWhenRanksAreNull() {
            mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

            int subject = rolesService.Sort("Trainee", "Rifleman");

            subject.Should().Be(0);
        }

        [Theory, InlineData(3, "Trainee"), InlineData(0, "Marksman")]
        public void ShouldGetUnitRoleByOrder(int order, string expected) {
            Role role1 = new Role {name = "Rifleman", order = 0, roleType = RoleType.INDIVIDUAL};
            Role role2 = new Role {name = "Gunner", order = 3, roleType = RoleType.INDIVIDUAL};
            Role role3 = new Role {name = "Marksman", order = 0, roleType = RoleType.UNIT};
            Role role4 = new Role {name = "Trainee", order = 3, roleType = RoleType.UNIT};
            Role role5 = new Role {name = "Gunner", order = 2, roleType = RoleType.INDIVIDUAL};
            List<Role> mockCollection = new List<Role> {role1, role2, role3, role4, role5};

            mockRolesDataService.Setup(x => x.Get()).Returns(mockCollection);
            mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<Func<Role, bool>>())).Returns<Func<Role, bool>>(x => mockCollection.FirstOrDefault(x));

            Role subject = rolesService.GetUnitRoleByOrder(order);

            subject.name.Should().Be(expected);
        }

        [Fact]
        public void ShouldReturnNullWhenNoUnitRoleFound() {
            mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<Func<Role, bool>>())).Returns<Func<Role, bool>>(null);

            Role subject = rolesService.GetUnitRoleByOrder(2);

            subject.Should().BeNull();
        }
    }
}
