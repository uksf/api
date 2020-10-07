using FluentAssertions;
using UKSF.Api.Services.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class RoleAttributeTests {
        [Theory, InlineData("ADMIN,PERSONNEL", RoleDefinitions.ADMIN, RoleDefinitions.PERSONNEL), InlineData("ADMIN", RoleDefinitions.ADMIN), InlineData("ADMIN", RoleDefinitions.ADMIN, RoleDefinitions.ADMIN)]
        public void ShouldCombineRoles(string expected, params string[] roles) {
            RolesAttribute rolesAttribute = new RolesAttribute(roles);

            string subject = rolesAttribute.Roles;

            subject.Should().Be(expected);
        }
    }
}
