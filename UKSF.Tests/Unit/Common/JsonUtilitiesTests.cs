using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common;

public class JsonUtilitiesTests
{
    [Fact]
    public void ShouldCopyComplexObject()
    {
        TestDataModel testDataModel1 = new() { Name = "1" };
        TestDataModel testDataModel2 = new() { Name = "2" };
        TestDataModel testDataModel3 = new() { Name = "3" };
        TestComplexDataModel testComplexDataModel = new()
        {
            Name = "Test", Data = testDataModel1, List = new() { "a", "b", "c" }, DataList = new() { testDataModel1, testDataModel2, testDataModel3 }
        };

        var subject = testComplexDataModel.Copy<TestComplexDataModel>();

        subject.Id.Should().Be(testComplexDataModel.Id);
        subject.Name.Should().Be(testComplexDataModel.Name);
        subject.Data.Should().NotBe(testDataModel1);
        subject.List.Should().HaveCount(3).And.Contain(new List<string> { "a", "b", "c" });
        subject.DataList.Should().HaveCount(3).And.NotContain(new List<TestDataModel> { testDataModel1, testDataModel2, testDataModel3 });
    }

    [Fact]
    public void ShouldCopyObject()
    {
        TestDataModel testDataModel = new() { Name = "Test" };

        var subject = testDataModel.Copy<TestDataModel>();

        subject.Id.Should().Be(testDataModel.Id);
        subject.Name.Should().Be(testDataModel.Name);
    }

    [Fact]
    public void ShouldEscapeJsonString()
    {
        const string UNESCAPED_JSON = "JSON:{\"message\": \"\\nMaking zeus \\ at 'C:\\test\\path'\", \"colour\": \"#20d18b\"}";

        var subject = UNESCAPED_JSON.Escape();

        subject.Should().Be("JSON:{\"message\": \"\\\\nMaking zeus \\\\ at 'C:\\\\test\\\\path'\", \"colour\": \"#20d18b\"}");
    }
}
