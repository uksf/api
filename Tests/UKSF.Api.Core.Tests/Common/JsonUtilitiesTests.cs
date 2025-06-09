using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Core.Tests.Common;

public class JsonUtilitiesTests
{
    [Fact]
    public void ShouldCopyComplexObject()
    {
        DomainTestModel testModel1 = new() { Name = "1" };
        DomainTestModel testModel2 = new() { Name = "2" };
        DomainTestModel testModel3 = new() { Name = "3" };
        DomainTestComplexModel testComplexModel = new()
        {
            Name = "Test",
            Data = testModel1,
            List = ["a", "b", "c"],
            DataList = [testModel1, testModel2, testModel3]
        };

        var subject = testComplexModel.Copy<DomainTestComplexModel>();

        subject.Id.Should().Be(testComplexModel.Id);
        subject.Name.Should().Be(testComplexModel.Name);
        subject.Data.Should().NotBe(testModel1);
        subject.List.Should()
        .HaveCount(3)
        .And.Contain(
            new List<string>
            {
                "a",
                "b",
                "c"
            }
        );
        subject.DataList.Should()
        .HaveCount(3)
        .And.NotContain(
            new List<DomainTestModel>
            {
                testModel1,
                testModel2,
                testModel3
            }
        );
    }

    [Fact]
    public void ShouldCopyObject()
    {
        DomainTestModel testModel = new() { Name = "Test" };

        var subject = testModel.Copy<DomainTestModel>();

        subject.Id.Should().Be(testModel.Id);
        subject.Name.Should().Be(testModel.Name);
    }

    [Fact]
    public void ShouldEscapeJsonString()
    {
        const string UnescapedJson = "JSON:{\"message\": \"\\nMaking zeus \\ at 'C:\\test\\path'\", \"colour\": \"#20d18b\"}";

        var subject = UnescapedJson.Escape();

        subject.Should().Be("JSON:{\"message\": \"\\\\nMaking zeus \\\\ at 'C:\\\\test\\\\path'\", \"colour\": \"#20d18b\"}");
    }
}
