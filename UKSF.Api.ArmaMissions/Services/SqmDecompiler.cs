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
                                           .WithTimeout(TimeSpan.FromMinutes(2))
                                           .WithRedirectStderrToOutput();

        var outputLines = new List<string>();
        await foreach (var line in command.ExecuteAsync())
        {
            if (line.Type == ProcessOutputType.Output && !string.IsNullOrEmpty(line.Content))
            {
                outputLines.Add(line.Content);
            }
        }

        if (File.Exists($"{sqmPath}.txt"))
        {
            File.Delete(sqmPath);
            File.Move($"{sqmPath}.txt", sqmPath);
        }
        else
        {
            var output = string.Join("\n", outputLines);
            throw new InvalidOperationException(string.IsNullOrEmpty(output) ? "DeRapDos failed: output file not found" : $"DeRapDos failed: {output}");
        }
    }
}
