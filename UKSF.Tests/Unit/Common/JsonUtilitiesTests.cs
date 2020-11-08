using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.Base.Extensions;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class JsonUtilitiesTests {
        [Fact]
        public void ShouldCopyComplexObject() {
            TestDataModel testDataModel1 = new TestDataModel {Name = "1"};
            TestDataModel testDataModel2 = new TestDataModel {Name = "2"};
            TestDataModel testDataModel3 = new TestDataModel {Name = "3"};
            TestComplexDataModel testComplexDataModel = new TestComplexDataModel {Name = "Test", Data = testDataModel1, List = new List<string> {"a", "b", "c"}, DataList = new List<TestDataModel> {testDataModel1, testDataModel2, testDataModel3}};

            TestComplexDataModel subject = testComplexDataModel.Copy<TestComplexDataModel>();

            subject.id.Should().Be(testComplexDataModel.id);
            subject.Name.Should().Be(testComplexDataModel.Name);
            subject.Data.Should().NotBe(testDataModel1);
            subject.List.Should().HaveCount(3).And.Contain(new List<string> {"a", "b", "c"});
            subject.DataList.Should().HaveCount(3).And.NotContain(new List<TestDataModel> {testDataModel1, testDataModel2, testDataModel3});
        }

        [Fact]
        public void ShouldCopyObject() {
            TestDataModel testDataModel = new TestDataModel {Name = "Test"};

            TestDataModel subject = testDataModel.Copy<TestDataModel>();

            subject.id.Should().Be(testDataModel.id);
            subject.Name.Should().Be(testDataModel.Name);
        }


        [Fact]
        public void ShouldEscapeJsonString() {
            const string UNESCAPED_JSON = "JSON:{\"message\": \"\\nMaking zeus \\ at 'C:\\test\\path'\", \"colour\": \"#20d18b\"}";

            string subject = UNESCAPED_JSON.Escape();

            subject.Should().Be("JSON:{\"message\": \"\\\\nMaking zeus \\\\ at 'C:\\\\test\\\\path'\", \"colour\": \"#20d18b\"}");
        }
    }
}
