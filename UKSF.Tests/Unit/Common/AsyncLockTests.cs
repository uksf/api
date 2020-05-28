using System;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Common {
    public class AsyncLockTests {
        [Fact]
        public void ShouldGetLock() {
            AsyncLock subject = new AsyncLock();

            Func<Task> act = async () => await subject.LockAsync();

            act.Should().CompleteWithinAsync(TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void ShouldWaitForLock() {
            AsyncLock subject = new AsyncLock();

            async Task Act1() {
                using (await subject.LockAsync()) {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            Func<Task> act2 = async () => { await subject.LockAsync(); };

            Task unused = Act1();
            act2.ExecutionTime().Should().BeGreaterThan(TimeSpan.FromSeconds(1));
        }
    }
}
