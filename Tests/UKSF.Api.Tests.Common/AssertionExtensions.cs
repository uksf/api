using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Tests.Common;

public static class AssertionExtensions
{
    extension<T>(Task<ExceptionAssertions<T>> task) where T : UksfException
    {
        public async Task WithMessageAndStatusCode(string expectedWildcardPattern, int statusCode)
        {
            (await task).WithMessage(expectedWildcardPattern).And.StatusCode.Should().Be(statusCode);
        }
    }

    extension<T>(ExceptionAssertions<T> assertion) where T : UksfException
    {
        public void WithMessageAndStatusCode(string expectedWildcardPattern, int statusCode)
        {
            assertion.WithMessage(expectedWildcardPattern).And.StatusCode.Should().Be(statusCode);
        }
    }
}
