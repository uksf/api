using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.Core.Extensions;
using Xunit;

namespace UKSF.Api.Core.Tests.Common;

public class CollectionUtilitiesTests
{
    [Fact]
    public void Should_remove_empty_strings_from_hashset()
    {
        HashSet<string> subject = ["1", "", null, "3"];

        subject.CleanHashset();

        subject.Should().BeEquivalentTo(new HashSet<string> { "1", "3" });
    }
}
