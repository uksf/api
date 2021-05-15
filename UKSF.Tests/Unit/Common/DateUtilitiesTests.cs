using System;
using FluentAssertions;
using UKSF.Api.Personnel.Extensions;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Common
{
    public class DateUtilitiesTests
    {
        [Fact]
        public void ShouldGiveCorrectMonthsForDay()
        {
            DateTime dob = new(2019, 1, 20);

            ApplicationAge age = dob.ToAge(new DateTime(2020, 1, 16));

            age.Months.Should().Be(11);
        }

        [Theory, InlineData(25, 4, 25, 4), InlineData(25, 13, 26, 1)]
        public void ShouldGiveCorrectAge(int years, int months, int expectedYears, int expectedMonths)
        {
            DateTime dob = DateTime.Today.AddYears(-years).AddMonths(-months);

            ApplicationAge age = dob.ToAge();

            age.Years.Should().Be(expectedYears);
            age.Months.Should().Be(expectedMonths);
        }
    }
}
