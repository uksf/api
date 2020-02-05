using System;
using System.Diagnostics;
using System.Management;
using Microsoft.Win32.TaskScheduler;
using Task = System.Threading.Tasks.Task;

namespace UKSF.Common {
    public static class ProcessUtilities {
        private const int SC_CLOSE = 0xF060;
        private const int WM_SYSCOMMAND = 0x0112;

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

        public static async Task LaunchExternalProcess(string name, string command) {
            TaskService.Instance.RootFolder.DeleteTask(name, false);
            using TaskDefinition taskDefinition = TaskService.Instance.NewTask();
            taskDefinition.Actions.Add(new ExecAction("cmd", $"/C {command}"));
            taskDefinition.Triggers.Add(new TimeTrigger(DateTime.Now.AddSeconds(1)));
            TaskService.Instance.RootFolder.RegisterTaskDefinition(name, taskDefinition);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        public static async Task CloseProcessGracefully(this Process process) {
            // UKSF.PostMessage exe location should be set as a PATH variable
            await LaunchExternalProcess("CloseProcess", $"start \"\" \"UKSF.PostMessage\" {process.ProcessName} {WM_SYSCOMMAND} {SC_CLOSE} 0");
        }
    }
}
