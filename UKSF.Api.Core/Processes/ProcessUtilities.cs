using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management;
using Microsoft.Win32.TaskScheduler;
using Task = System.Threading.Tasks.Task;

namespace UKSF.Api.Core.Processes;

public interface IProcessUtilities
{
    int LaunchManagedProcess(string executable, string arguments = null);
    Task LaunchExternalProcess(string name, string command, string workingDirectory = null);
    Task CloseProcessGracefully(Process process);
    Process FindProcessById(int id);
    Process FindProcessByName(string name);
    Process[] GetProcessesByName(string name);
    Process[] GetProcesses();
}

[ExcludeFromCodeCoverage]
public class ProcessUtilities : IProcessUtilities
{
    private const int ScClose = 0xF060;
    private const int WmSysCommand = 0x0112;

    public int LaunchManagedProcess(string executable, string arguments = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("Not running on windows, stopping");
        }

        var processId = 0;
        using ManagementClass managementClass = new("Win32_Process");
        ManagementClass processInfo = new("Win32_ProcessStartup");
        processInfo.Properties["CreateFlags"].Value = 0x00000008;

        var inParameters = managementClass.GetMethodParameters("Create");
        inParameters["CommandLine"] = $"\"{executable}\" {arguments}";
        inParameters["ProcessStartupInformation"] = processInfo;

        var result = managementClass.InvokeMethod("Create", inParameters, null);
        if (result is not null && (uint)result.Properties["ReturnValue"].Value == 0)
        {
            processId = Convert.ToInt32(result.Properties["ProcessId"].Value.ToString());
        }

        return processId;
    }

    public async Task LaunchExternalProcess(string name, string command, string workingDirectory = null)
    {
        TaskService.Instance.RootFolder.DeleteTask(name, false);
        using var taskDefinition = TaskService.Instance.NewTask();
        taskDefinition.Actions.Add(new ExecAction("cmd", $"/C {command}", workingDirectory));
        taskDefinition.Triggers.Add(new TimeTrigger(DateTime.UtcNow.AddSeconds(1)));
        TaskService.Instance.RootFolder.RegisterTaskDefinition(name, taskDefinition);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async Task CloseProcessGracefully(Process process)
    {
        // UKSF.PostMessage exe location should be set as a PATH variable
        await LaunchExternalProcess("CloseProcess", $"start \"\" \"UKSF.PostMessage\" {process.ProcessName} {WmSysCommand} {ScClose} 0");
    }

    public Process FindProcessById(int id)
    {
        return Process.GetProcesses().FirstOrDefault(x => x.Id == id);
    }

    public Process FindProcessByName(string name)
    {
        return Process.GetProcesses().FirstOrDefault(x => x.ProcessName == name);
    }

    public Process[] GetProcessesByName(string name)
    {
        return Process.GetProcessesByName(name);
    }

    public Process[] GetProcesses()
    {
        return Process.GetProcesses();
    }
}
