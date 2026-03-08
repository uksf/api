using Microsoft.Extensions.Configuration;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.ArmaMissions.Services;

public class SqmDecompiler(IProcessCommandFactory processCommandFactory, IConfiguration configuration) : ISqmDecompiler
{
    private const string DefaultDeRapDosPath = @"C:\Program Files (x86)\Mikero\DePboTools\bin\DeRapDos.exe";

    private string DeRapDosPath => configuration.GetValue("MissionPatching:DeRapDosPath", DefaultDeRapDosPath);

    public async Task<bool> IsBinarized(string sqmPath)
    {
        var command = processCommandFactory.CreateCommand(DeRapDosPath, Path.GetDirectoryName(sqmPath) ?? ".", $"-p -q \"{sqmPath}\"")
                                           .WithTimeout(TimeSpan.FromMinutes(2));

        var exitCode = 0;
        await foreach (var line in command.ExecuteAsync())
        {
            if (line.Type == ProcessOutputType.ProcessCompleted)
            {
                exitCode = line.ExitCode;
            }
        }

        return exitCode == 0;
    }

    public async Task Decompile(string sqmPath)
    {
        var command = processCommandFactory.CreateCommand(DeRapDosPath, Path.GetDirectoryName(sqmPath) ?? ".", $"-p \"{sqmPath}\"")
                                           .WithTimeout(TimeSpan.FromMinutes(2));

        await foreach (var line in command.ExecuteAsync())
        {
            if (line.Type == ProcessOutputType.Error)
            {
                throw new InvalidOperationException($"DeRapDos failed: {line.Content}");
            }
        }

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
