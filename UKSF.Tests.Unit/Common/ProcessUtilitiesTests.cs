using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
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

        [Fact]
        public async Task ShouldCloseProcess() {
            const string NAME = "Test";
            const string COMMAND = "timeout 5";
            string expected = $"C:\\Windows\\system32\\cmd.EXE /C {COMMAND}";

            await ProcessUtilities.LaunchExternalProcess(NAME, COMMAND);
            await Task.Delay(TimeSpan.FromSeconds(1));

            Process subject = Process.GetProcessesByName("cmd").FirstOrDefault(x => {
                using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + x.Id);
                using ManagementObjectCollection objects = searcher.Get();
                return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString() == expected;
            });

            subject.Should().NotBeNull();

            await subject.CloseProcessGracefully();
            await Task.Delay(TimeSpan.FromSeconds(1));

            subject?.HasExited.Should().BeTrue();
        }
    }
}
