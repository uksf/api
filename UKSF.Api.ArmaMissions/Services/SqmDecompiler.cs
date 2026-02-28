using System.Diagnostics;

namespace UKSF.Api.ArmaMissions.Services;

public class SqmDecompiler : ISqmDecompiler
{
    private const string Unbin = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\DeRapDos.exe";

    public async Task<bool> IsBinarized(string sqmPath)
    {
        Process process = new()
        {
            StartInfo =
            {
                FileName = Unbin,
                Arguments = $"-p -q \"{sqmPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    public async Task Decompile(string sqmPath)
    {
        Process process = new()
        {
            StartInfo =
            {
                FileName = Unbin,
                Arguments = $"-p \"{sqmPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();

        if (File.Exists($"{sqmPath}.txt"))
        {
            File.Delete(sqmPath);
            File.Move($"{sqmPath}.txt", sqmPath);
        }
        else
        {
            throw new FileNotFoundException();
        }
    }
}
