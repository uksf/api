using System;
using System.Collections.Generic;
using FluentAssertions;
using UKSF.Common;
using UKSF.Tests.Unit.Common;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Common {
    public class DataUtilitiesTests {
        [Fact]
        public void ShouldReturnIdValueForValidObject() {
            MockDataModel mockDataModel = new MockDataModel();

            string subject = mockDataModel.GetIdValue();

            subject.Should().Be(mockDataModel.id);
        }

        [Fact]
        public void ShouldReturnEmptyStringForInvalidObject() {
            DateTime dateTime = new DateTime();

            string subject = dateTime.GetIdValue();

            subject.Should().Be(string.Empty);
        }

        [Fact]
        public void ShouldReturnIdWithinOneSecond() {
            MockDataModel mockDataModel = new MockDataModel { Stuff = new List<object>() };
            for (int i = 0; i < 10000; i++) {
                mockDataModel.Stuff.Add(new {index = i, data = Guid.NewGuid(), number = i * 756 * 458 * 5478});
            }

            Action act = () => mockDataModel.GetIdValue();

            act.ExecutionTime().Should().BeLessThan(TimeSpan.FromSeconds(1));
        }
    }
}
