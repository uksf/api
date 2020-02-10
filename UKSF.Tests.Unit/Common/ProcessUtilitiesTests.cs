using System;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Microsoft.Win32.TaskScheduler;
using UKSF.Common;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UKSF.Tests.Unit.Common {
    public class ProcessUtilitiesTests {
        [Fact]
        public void ShouldLaunchManagedProcess() {
            int processId = ProcessUtilities.LaunchManagedProcess("cmd", "/C timeout 1");

            Process subject = Process.GetProcessById(processId);

            subject.Id.Should().Be(processId);

            subject.Kill();
        }

        [Fact]
        public async Task ShouldCreateTask() {
            const string NAME = "Test";
            await ProcessUtilities.LaunchExternalProcess(NAME, "exit");

            TaskService.Instance.RootFolder.Tasks.Should().Contain(x => x.Name == NAME);

            TaskService.Instance.RootFolder.DeleteTask(NAME, false);
        }

        [Fact]
        public async Task ShouldRunTask() {
            const string NAME = "Test";
            await ProcessUtilities.LaunchExternalProcess(NAME, "exit");

            TaskService.Instance.RootFolder.Tasks.First(x => x.Name == NAME).LastRunTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));

            TaskService.Instance.RootFolder.DeleteTask(NAME, false);
        }
    }
}
