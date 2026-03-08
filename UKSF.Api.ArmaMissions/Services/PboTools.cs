using Microsoft.Extensions.Configuration;
using UKSF.Api.ArmaMissions.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.ArmaMissions.Services;

public class PboTools(IProcessCommandFactory processCommandFactory, IConfiguration configuration) : IPboTools
{
    private const string DefaultExtractPboDosPath = @"C:\Program Files (x86)\Mikero\DePboTools\bin\ExtractPboDos.exe";
    private const string DefaultMakePboPath = @"C:\Program Files (x86)\Mikero\DePboTools\bin\MakePbo.exe";
    private const string DefaultSimplePackPboPath = @"C:\Program Files\PBO Manager v.1.4 beta\PBOConsole.exe";

    private string ExtractPboDosPath => configuration.GetValue("MissionPatching:ExtractPboDosPath", DefaultExtractPboDosPath);
    private string MakePboToolPath => configuration.GetValue("MissionPatching:MakePboPath", DefaultMakePboPath);
    private string SimplePackPboToolPath => configuration.GetValue("MissionPatching:PBOConsolePath", DefaultSimplePackPboPath);

    public async Task ExtractPbo(string pboPath, string parentFolder)
    {
        var folderPath = Path.Combine(parentFolder, Path.GetFileNameWithoutExtension(pboPath));
        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, true);
        }

        var command = processCommandFactory.CreateCommand(ExtractPboDosPath, parentFolder, $"-D -P \"{pboPath}\"").WithTimeout(TimeSpan.FromMinutes(2));

        await foreach (var line in command.ExecuteAsync())
        {
            if (line.Type == ProcessOutputType.Error)
            {
                throw new PboOperationException($"ExtractPboDos failed: {line.Content}");
            }
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("Could not find unpacked pbo");
        }
    }

    public async Task MakePbo(string folderPath, string pboPath, string workingDirectory)
    {
        var command = processCommandFactory
                      .CreateCommand(
                          MakePboToolPath,
                          workingDirectory,
                          $"-Z -BD -P -X=\"thumbs.db,*.txt,*.h,*.dep,*.cpp,*.bak,*.png,*.log,*.pew\" \"{folderPath}\""
                      )
                      .WithTimeout(TimeSpan.FromMinutes(2))
                      .WithRedirectStderrToOutput();

        var outputLines = new List<string>();
        await foreach (var line in command.ExecuteAsync())
        {
            if (line.Type == ProcessOutputType.Output && !string.IsNullOrEmpty(line.Content) && !line.Content.ContainsIgnoreCase("compressing"))
            {
                outputLines.Add(line.Content);
            }
        }

        if (File.Exists(pboPath))
        {
            return;
        }

        var output = string.Join("\n", outputLines);
        throw new PboOperationException(output);
    }

    public async Task SimplePackPbo(string folderPath, string pboPath, string workingDirectory)
    {
        var command = processCommandFactory.CreateCommand(SimplePackPboToolPath, workingDirectory, $"-pack \"{folderPath}\" \"{pboPath}\"")
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

        if (File.Exists(pboPath))
        {
            File.Delete($"{pboPath}.bak");
            return;
        }

        var output = string.Join("\n", outputLines);
        throw new PboOperationException(output);
    }
}
