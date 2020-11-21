using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class RolesServiceTests {
        private readonly Mock<IRolesContext> _mockRolesDataService;
        private readonly RolesService _rolesService;

        public RolesServiceTests() {
            _mockRolesDataService = new Mock<IRolesContext>();
            _rolesService = new RolesService(_mockRolesDataService.Object);
        }

        [Theory, InlineData("Trainee", "Rifleman", 1), InlineData("Rifleman", "Trainee", -1), InlineData("Rifleman", "Rifleman", 0)]
        public void ShouldGetCorrectSortValueByName(string nameA, string nameB, int expected) {
            Role role1 = new() { Name = "Rifleman", Order = 0 };
            Role role2 = new() { Name = "Trainee", Order = 1 };
            List<Role> mockCollection = new() { role1, role2 };

            _mockRolesDataService.Setup(x => x.Get()).Returns(mockCollection);
            _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

            int subject = _rolesService.Sort(nameA, nameB);

            subject.Should().Be(expected);
        }

        [Theory, InlineData(3, "Trainee"), InlineData(0, "Marksman")]
        public void ShouldGetUnitRoleByOrder(int order, string expected) {
            Role role1 = new() { Name = "Rifleman", Order = 0, RoleType = RoleType.INDIVIDUAL };
            Role role2 = new() { Name = "Gunner", Order = 3, RoleType = RoleType.INDIVIDUAL };
            Role role3 = new() { Name = "Marksman", Order = 0, RoleType = RoleType.UNIT };
            Role role4 = new() { Name = "Trainee", Order = 3, RoleType = RoleType.UNIT };
            Role role5 = new() { Name = "Gunner", Order = 2, RoleType = RoleType.INDIVIDUAL };
            List<Role> mockCollection = new() { role1, role2, role3, role4, role5 };

            _mockRolesDataService.Setup(x => x.Get()).Returns(mockCollection);
            _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<Func<Role, bool>>())).Returns<Func<Role, bool>>(x => mockCollection.FirstOrDefault(x));

            Role subject = _rolesService.GetUnitRoleByOrder(order);

            subject.Name.Should().Be(expected);
        }

        [Fact]
        public void ShouldReturnNullWhenNoUnitRoleFound() {
            _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<Func<Role, bool>>())).Returns<Func<Role, bool>>(null);

            Role subject = _rolesService.GetUnitRoleByOrder(2);

            subject.Should().BeNull();
        }

        [Fact]
        public void ShouldReturnZeroForSortWhenRanksAreNull() {
            _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

            int subject = _rolesService.Sort("Trainee", "Rifleman");

            subject.Should().Be(0);
        }
    }
}
