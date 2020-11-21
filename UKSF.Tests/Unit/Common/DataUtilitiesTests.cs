using FluentAssertions;
using Newtonsoft.Json.Linq;
using UKSF.Api.Shared.Extensions;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class DataUtilitiesTests {
        [Fact]
        public void Should_return_correct_value_from_body() {
            JObject jObject = JObject.Parse("{\"key1\":\"item1\", \"key2\":\"item2\"}");

            string subject = jObject.GetValueFromBody("key2");

            subject.Should().Be("item2");
        }

        [Fact]
        public void Should_return_nothing_from_body_for_invalid_key() {
            JObject jObject = JObject.Parse("{\"key\":\"value\"}");

            string subject = jObject.GetValueFromBody("notthekey");

            subject.Should().Be(string.Empty);
        }

        [Fact]
        public void Should_return_value_as_string_from_body_when_data_is_not_string() {
            JObject jObject = JObject.Parse("{\"key\":2}");

            string subject = jObject.GetValueFromBody("key");

            subject.Should().Be("2");
        }
    }
}
