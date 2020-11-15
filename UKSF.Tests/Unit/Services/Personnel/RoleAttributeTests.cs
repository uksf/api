using FluentAssertions;
using UKSF.Api.Shared;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class RoleAttributeTests {
        [Theory, InlineData("ADMIN,PERSONNEL", Permissions.ADMIN, Permissions.PERSONNEL), InlineData("ADMIN", Permissions.ADMIN), InlineData("ADMIN", Permissions.ADMIN, Permissions.ADMIN)]
        public void ShouldCombineRoles(string expected, params string[] roles) {
            PermissionsAttribute permissionsAttribute = new PermissionsAttribute(roles);

            string subject = permissionsAttribute.Roles;

            subject.Should().Be(expected);
        }
    }
}
