using FluentAssertions;
using UKSF.Api.Core.Extensions;
using Xunit;

namespace UKSF.Api.Core.Tests.Common;

public class GuardUtilitiesTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("1", false)]
    [InlineData("5ed43018bea2f1945440f37d", true)]
    public void ShouldValidateIdCorrectly(string id, bool valid)
    {
        var subject = true;

        GuardUtilities.ValidateId(id, _ => subject = false);

        subject.Should().Be(valid);
    }

    [Theory]
    [InlineData(new[] { 2, 4, 6, 8, 10, 12 }, false)]
    [InlineData(new[] { 2, 4, 5, 6, 8 }, false)]
    [InlineData(new[] { 2, 4, 6, 8, 10 }, true)]
    public void ShouldValidateArrayCorrectly(int[] array, bool valid)
    {
        var subject = true;

        GuardUtilities.ValidateArray(array, x => x.Length == 5, x => x % 2 == 0, () => subject = false);

        subject.Should().Be(valid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("1", true)]
    public void ShouldValidateStringCorrectly(string text, bool valid)
    {
        var subject = true;

        GuardUtilities.ValidateString(text, _ => subject = false);

        subject.Should().Be(valid);
    }

    [Theory]
    [InlineData(new[] { "" }, false, false)]
    [InlineData(new[] { "", "2" }, true, false)]
    [InlineData(new[] { "5ed43018bea2f1945440f37d", "2" }, true, false)]
    [InlineData(new[] { "5ed43018bea2f1945440f37d", "5ed43018bea2f1945440f37e" }, true, true)]
    public void ShouldValidateIdArrayCorrectly(string[] array, bool valid, bool idValid)
    {
        var subject = true;
        var subjectId = true;

        GuardUtilities.ValidateIdArray(array, x => x.Length == 2, () => subject = false, _ => subjectId = false);

        subject.Should().Be(valid);
        subjectId.Should().Be(idValid);
    }
}
