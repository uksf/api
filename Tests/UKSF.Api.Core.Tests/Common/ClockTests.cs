using System;
using FluentAssertions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Common;

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

        subject.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void Should_return_current_utc_date_and_time()
    {
        var subject = new Clock().UtcNow();

        subject.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(10));
    }

    [Theory]
    // GMT (winter): UK time == UTC
    [InlineData("2026-02-27T03:00:00Z", new[] { 6, 18 }, "2026-02-27T06:00:00Z")]
    [InlineData("2026-02-27T10:00:00Z", new[] { 6, 18 }, "2026-02-27T18:00:00Z")]
    [InlineData("2026-02-27T22:00:00Z", new[] { 6, 18 }, "2026-02-28T06:00:00Z")]
    // BST (summer): UK time == UTC + 1
    [InlineData("2026-04-06T03:00:00Z", new[] { 6, 18 }, "2026-04-06T05:00:00Z")]
    [InlineData("2026-04-06T10:00:00Z", new[] { 6, 18 }, "2026-04-06T17:00:00Z")]
    [InlineData("2026-04-06T22:00:00Z", new[] { 6, 18 }, "2026-04-07T05:00:00Z")]
    // Single hour daily schedule
    [InlineData("2026-04-06T10:00:00Z", new[] { 2 }, "2026-04-07T01:00:00Z")]
    [InlineData("2026-02-27T00:30:00Z", new[] { 2 }, "2026-02-27T02:00:00Z")]
    // DST spring-forward: last GMT day → first BST day
    [InlineData("2026-03-28T18:00:00Z", new[] { 6, 18 }, "2026-03-29T05:00:00Z")]
    // DST autumn-back: last BST day → first GMT day
    [InlineData("2026-10-24T17:00:00Z", new[] { 6, 18 }, "2026-10-25T06:00:00Z")]
    // Midnight schedule
    [InlineData("2026-04-06T10:00:00Z", new[] { 0, 12 }, "2026-04-06T11:00:00Z")]
    public void NextUkHourUtc_ShouldReturnNextUkHourSlotAsUtc(string previousIso, int[] ukHours, string expectedIso)
    {
        var previous = DateTime.Parse(previousIso, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var expected = DateTime.Parse(expectedIso, null, System.Globalization.DateTimeStyles.RoundtripKind);

        var result = new Clock().NextUkHourUtc(previous, ukHours);

        result.Should().Be(expected);
    }

    [Fact]
    public void NextUkHourUtc_WithNoHours_ShouldThrow()
    {
        var act = () => new Clock().NextUkHourUtc(DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
