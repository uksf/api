using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base;
using UKSF.Api.Shared.Context;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Shared.Tests {
    public class DependencyInjectionTests : DependencyInjectionTestsBase {
        public DependencyInjectionTests() {
            Services.AddUksfBase(TestConfiguration);
            Services.AddUksfShared();
        }

        [Fact]
        public void When_resolving_LogContext() {
            Services.AddTransient<ILogContext, LogContext>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            ILogContext subject = serviceProvider.GetRequiredService<ILogContext>();

            subject.Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_AuditLogContext() {
            Services.AddTransient<IAuditLogContext, AuditLogContext>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            IAuditLogContext subject = serviceProvider.GetRequiredService<IAuditLogContext>();

            subject.Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_HttpErrorLogContext() {
            Services.AddTransient<IHttpErrorLogContext, HttpErrorLogContext>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            IHttpErrorLogContext subject = serviceProvider.GetRequiredService<IHttpErrorLogContext>();

            subject.Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_LauncherLogContext() {
            Services.AddTransient<ILauncherLogContext, LauncherLogContext>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            ILauncherLogContext subject = serviceProvider.GetRequiredService<ILauncherLogContext>();

            subject.Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_DiscordLogContext() {
            Services.AddTransient<IDiscordLogContext, DiscordLogContext>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            IDiscordLogContext subject = serviceProvider.GetRequiredService<IDiscordLogContext>();

            subject.Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_SchedulerContext() {
            Services.AddTransient<ISchedulerContext, SchedulerContext>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            ISchedulerContext subject = serviceProvider.GetRequiredService<ISchedulerContext>();

            subject.Should().NotBeNull();
        }
    }
}
