﻿using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.AppStart;
using UKSF.Api.Controllers;
using UKSF.Api.EventHandlers;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Tests {
    public class DependencyInjectionTests : DependencyInjectionTestsBase {
        public DependencyInjectionTests() {
            Services.AddUksf(Configuration, HostEnvironment);
        }

        [Fact]
        public void When_resolving_controllers() {
            Services.AddTransient<LoaController>();
            Services.AddTransient<LoggingController>();
            Services.AddTransient<ModsController>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<LoaController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<LoggingController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<ModsController>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_event_handlers() {
            Services.AddTransient<LoggerEventHandler>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<LoggerEventHandler>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_filters() {
            Services.AddTransient<ExceptionHandler>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<ExceptionHandler>().Should().NotBeNull();
        }
    }
}