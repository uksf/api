using System;
using FluentAssertions;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class DateUtilitiesTests {
        [Theory, InlineData(25, 4, 25, 4), InlineData(25, 13, 26, 1)]
        public void ShouldGiveCorrectAge(int years, int months, int expectedYears, int expectedMonths) {
            DateTime dob = DateTime.Today.AddYears(-years).AddMonths(-months);

            (int subjectYears, int subjectMonths) = dob.ToAge();

            subjectYears.Should().Be(expectedYears);
            subjectMonths.Should().Be(expectedMonths);
        }

        [Fact]
        public void ShouldGiveCorrectMonths() {
            DateTime dob = new DateTime(2019, 1, 20);

            (int _, int subjectMonths) = dob.ToAge(new DateTime(2020, 1, 16));

            subjectMonths.Should().Be(11);
        }
    }
}
