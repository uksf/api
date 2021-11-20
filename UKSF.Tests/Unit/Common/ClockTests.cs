using System;
using FluentAssertions;
using UKSF.Api.Shared.Services;
using Xunit;

namespace UKSF.Tests.Unit.Common
{
    public class ClockTests
    {
        [Fact]
        public void Should_return_current_date()
        {
            var subject = new Clock().Today();

            subject.Should().BeCloseTo(DateTime.UtcNow.Date, TimeSpan.FromMilliseconds(0));
        }

        [Fact]
        public void Should_return_current_date_and_time()
        {
            var subject = new Clock().Now();

            subject.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(10));
        }

        [Fact]
        public void Should_return_current_utc_date_and_time()
        {
            var subject = new Clock().UtcNow();

            subject.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(10));
        }
    }
}
