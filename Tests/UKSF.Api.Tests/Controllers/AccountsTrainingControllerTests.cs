using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using UKSF.Api.Controllers;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class AccountsTrainingControllerTests
{
    [Fact]
    public void Every_endpoint_requires_command_permission()
    {
        var classAttribute = typeof(AccountsTrainingController).GetCustomAttribute<PermissionsAttribute>();
        classAttribute.Should().NotBeNull();
        classAttribute!.Roles.Should().Be(Permissions.Command);

        typeof(AccountsTrainingController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                          .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())
                                          .Should()
                                          .BeEmpty();
    }
}
