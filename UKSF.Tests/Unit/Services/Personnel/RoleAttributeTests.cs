using FluentAssertions;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel;

public class RoleAttributeTests
{
    [Theory]
    [InlineData("ADMIN,PERSONNEL", Permissions.Admin, Permissions.Personnel)]
    [InlineData("ADMIN", Permissions.Admin)]
    [InlineData("ADMIN", Permissions.Admin, Permissions.Admin)]
    public void ShouldCombineRoles(string expected, params string[] roles)
    {
        PermissionsAttribute permissionsAttribute = new(roles);

        var subject = permissionsAttribute.Roles;

        subject.Should().Be(expected);
    }
}
