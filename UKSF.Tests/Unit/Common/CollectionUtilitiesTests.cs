using System.Collections.Generic;
using FluentAssertions;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Common {
    public class CollectionUtilitiesTests {
        [Fact]
        public void ShouldCleanHashset() {
            HashSet<string> subject = new HashSet<string> {"1", "", "3"};

            subject.CleanHashset();

            subject.Should().BeEquivalentTo(new HashSet<string> { "1", "3" });
        }
    }
}
