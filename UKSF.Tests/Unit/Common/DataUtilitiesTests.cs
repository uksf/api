using System;
using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json.Linq;
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

        [Fact]
        public void ShouldGetCorrectValueFromBody() {
            JObject jObject = JObject.Parse("{\"key1\":\"item1\", \"key2\":\"item2\"}");

            string subject = jObject.GetValueFromBody("key2");

            subject.Should().Be("item2");
        }

        [Fact]
        public void ShouldGetValueAsStringFromBody() {
            JObject jObject = JObject.Parse("{\"key\":2}");

            string subject = jObject.GetValueFromBody("key");

            subject.Should().Be("2");
        }

        [Fact]
        public void ShouldReturnEmptyStringFromBodyForInvalidKey() {
            JObject jObject = JObject.Parse("{\"key\":\"value\"}");

            string subject = jObject.GetValueFromBody("notthekey");

            subject.Should().Be(string.Empty);
        }
    }
}
