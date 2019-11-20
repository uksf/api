using System;
using System.Management;
using Microsoft.Win32.TaskScheduler;
using Task = System.Threading.Tasks.Task;

namespace UKSFWebsite.Api.Services.Utility {
    public static class ProcessHelper {
        public static int LaunchManagedProcess(string executable, string arguments = null) {
            int processId = default;
            using ManagementClass managementClass = new ManagementClass("Win32_Process");
            ManagementClass processInfo = new ManagementClass("Win32_ProcessStartup");
            processInfo.Properties["CreateFlags"].Value = 0x00000008;

            ManagementBaseObject inParameters = managementClass.GetMethodParameters("Create");
            inParameters["CommandLine"] = $"\"{executable}\" {arguments}";
            inParameters["ProcessStartupInformation"] = processInfo;

            ManagementBaseObject result = managementClass.InvokeMethod("Create", inParameters, null);
            if (result != null && (uint) result.Properties["ReturnValue"].Value == 0) {
                processId = Convert.ToInt32(result.Properties["ProcessId"].Value.ToString());
            }

            return processId;
        }

        public static async Task LaunchProcess(string name, string command) {
            using TaskDefinition taskDefinition = TaskService.Instance.NewTask();
            taskDefinition.Actions.Add(new ExecAction("cmd", $"/C {command}"));
            taskDefinition.Triggers.Add(new TimeTrigger(DateTime.Now.AddSeconds(1)));
            TaskService.Instance.RootFolder.RegisterTaskDefinition(name, taskDefinition);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
