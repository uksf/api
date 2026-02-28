using System.Diagnostics;
using System.Text.RegularExpressions;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.ArmaMissions.Services;

public class PboTools : IPboTools
{
    private const string ExtractPboPath = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\ExtractPboDos.exe";
    private const string MakePboPath = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\MakePbo.exe";
    private const string SimplePackPboPath = "C:\\Program Files\\PBO Manager v.1.4 beta\\PBOConsole.exe";

    public async Task ExtractPbo(string pboPath, string parentFolder)
    {
        var folderPath = Path.Combine(parentFolder, Path.GetFileNameWithoutExtension(pboPath));
        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, true);
        }

        Process process = new()
        {
            StartInfo =
            {
                FileName = ExtractPboPath,
                Arguments = $"-D -P \"{pboPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("Could not find unpacked pbo");
        }
    }

    public async Task MakePbo(string folderPath, string pboPath, string workingDirectory)
    {
        Process process = new()
        {
            StartInfo =
            {
                FileName = MakePboPath,
                WorkingDirectory = workingDirectory,
                Arguments = $"-Z -BD -P -X=\"thumbs.db,*.txt,*.h,*.dep,*.cpp,*.bak,*.png,*.log,*.pew\" \"{folderPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (File.Exists(pboPath))
        {
            return;
        }

        var outputLines = Regex.Split($"{output}\n{errorOutput}", "\r\n|\r|\n").ToList();
        output = string.Join("\n", outputLines.Where(x => !string.IsNullOrEmpty(x) && !x.ContainsIgnoreCase("compressing")));
        throw new Exception(output);
    }

    public async Task SimplePackPbo(string folderPath, string pboPath, string workingDirectory)
    {
        Process process = new()
        {
            StartInfo =
            {
                FileName = SimplePackPboPath,
                WorkingDirectory = workingDirectory,
                Arguments = $"-pack \"{folderPath}\" \"{pboPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (File.Exists(pboPath))
        {
            File.Delete($"{pboPath}.bak");
            return;
        }

        var outputLines = Regex.Split($"{output}\n{errorOutput}", "\r\n|\r|\n").ToList();
        output = string.Join("\n", outputLines.Where(x => !string.IsNullOrEmpty(x)));
        throw new Exception(output);
    }
}
