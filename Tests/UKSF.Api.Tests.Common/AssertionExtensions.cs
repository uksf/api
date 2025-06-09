using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Tests.Common;

public static class AssertionExtensions
{
    public static async Task WithMessageAndStatusCode<T>(this Task<ExceptionAssertions<T>> task, string expectedWildcardPattern, int statusCode)
        where T : UksfException
    {
        (await task).WithMessage(expectedWildcardPattern).And.StatusCode.Should().Be(statusCode);
    }

    public static void WithMessageAndStatusCode<T>(this ExceptionAssertions<T> assertion, string expectedWildcardPattern, int statusCode)
        where T : UksfException
    {
        assertion.WithMessage(expectedWildcardPattern).And.StatusCode.Should().Be(statusCode);
    }
}
