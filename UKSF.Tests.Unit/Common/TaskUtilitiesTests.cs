using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class TaskUtilitiesTests {
        [Fact]
        public void ShouldDelay() {
            CancellationTokenSource token = new CancellationTokenSource();
            Action act = async () => await TaskUtilities.Delay(TimeSpan.FromMilliseconds(10), token.Token);

            act.ExecutionTime().Should().BeLessOrEqualTo(TimeSpan.FromMilliseconds(10));
        }

        [Fact]
        public void ShouldNotThrowExceptionForDelay() {
            Action act = () => {
                CancellationTokenSource token = new CancellationTokenSource();
                Task unused = TaskUtilities.Delay(TimeSpan.FromMilliseconds(50), token.Token);
                token.Cancel();
            };

            act.Should().NotThrow();
        }
    }
}
