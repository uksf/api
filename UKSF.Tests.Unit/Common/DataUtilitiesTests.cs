using System;
using FluentAssertions;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class DataUtilitiesTests {
        [Fact]
        public void ShouldReturnIdValue() {
            MockDataModel mockDataModel = new MockDataModel();

            string subject = mockDataModel.GetIdValue();

            subject.Should().Be(mockDataModel.id);
        }

        [Fact]
        public void ShouldReturnEmptyString() {
            DateTime dateTime = new DateTime();

            string subject = dateTime.GetIdValue();

            subject.Should().Be(string.Empty);
        }
    }
}
