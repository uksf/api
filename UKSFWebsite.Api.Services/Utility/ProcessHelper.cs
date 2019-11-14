using System.Diagnostics;
using System.Management;

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
                processId = (int) result.Properties["ProcessId"].Value;
            }

            return processId;
        }

        public static int LaunchProcess(string executable, string arguments = null) {
            using Process process = new Process {StartInfo = {UseShellExecute = true, FileName = executable, Arguments = arguments, Verb = "runas"}};
            process.Start();

            return process.Id;
        }
    }
}
