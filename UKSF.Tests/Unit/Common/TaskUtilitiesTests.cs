using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Common {
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

        [Fact]
        public void ShouldCallbackAfterDelay() {
            bool subject = false;
            Func<Task> act = async () => {
                CancellationTokenSource token = new CancellationTokenSource();
                await TaskUtilities.DelayWithCallback(
                    TimeSpan.FromMilliseconds(10),
                    token.Token,
                    () => {
                        subject = true;
                        return Task.CompletedTask;
                    }
                );
            };

            act.Should().NotThrow();
            act.ExecutionTime().Should().BeGreaterThan(TimeSpan.FromMilliseconds(10));
            subject.Should().BeTrue();
        }

        [Fact]
        public void ShouldNotCallbackForCancellation() {
            CancellationTokenSource token = new CancellationTokenSource();
            Func<Task> act = async () => {
                await TaskUtilities.DelayWithCallback(
                    TimeSpan.FromMilliseconds(10),
                    token.Token,
                    null
                );
            };

            act.Should().NotThrowAsync();
            token.Cancel();
            act.ExecutionTime().Should().BeLessThan(TimeSpan.FromMilliseconds(10));
        }
    }
}
