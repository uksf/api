using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Shared.ScheduledActions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Shared.Tests;

public class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests()
    {
        Services.AddUksfShared(Configuration, HostEnvironment);
    }

    [Fact]
    public void When_resolving_scheduled_actions()
    {
        Services.AddTransient<ActionDeleteExpiredConfirmationCode>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ActionDeleteExpiredConfirmationCode>().Should().NotBeNull();
    }
}
