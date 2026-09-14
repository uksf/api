using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.Services;

public class MongoDumpFile
{
    public string Database { get; set; }
    public string Path { get; set; }
    public long Bytes { get; set; }
}

public interface IMongoDumpService
{
    Task<List<MongoDumpFile>> Dump(string outputDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
///     Replaces the dump.bat scheduled task, which had been failing since 2026-03-11 while the backup that imaged its
///     output reported success. The password is written to a short-lived config file and deleted afterwards, so it
///     never appears in a process listing, and it is never logged.
/// </summary>
public class MongoDumpService(
    IVariablesService variablesService,
    IOptions<AppSettings> options,
    IProcessRunner processRunner,
    IFileSystemProvider fileSystemProvider,
    IUksfLogger logger
) : IMongoDumpService
{
    private const string DefaultMongoDumpPath = @"D:\Tools\MongoDBTools\mongodb-database-tools-windows-x86_64-100.17.0\bin\mongodump.exe";
    private const string ConfigFileName = "mongodump.conf";

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    private readonly AppSettings _appSettings = options.Value;

    public async Task<List<MongoDumpFile>> Dump(string outputDirectory, CancellationToken cancellationToken = default)
    {
        var mongoUrl = ResolveMongoUrl();
        var executable = variablesService.GetVariable("BACKUP_MONGODUMP_PATH").AsStringWithDefault(DefaultMongoDumpPath);
        var databases = ResolveDatabases();

        fileSystemProvider.CreateDirectory(outputDirectory);

        var configPath = Path.Combine(outputDirectory, ConfigFileName);
        fileSystemProvider.WriteAllText(configPath, $"password: {mongoUrl.Password}{Environment.NewLine}");

        try
        {
            var files = new List<MongoDumpFile>();
            foreach (var database in databases)
            {
                files.Add(await DumpDatabase(mongoUrl, executable, configPath, outputDirectory, database, cancellationToken));
            }

            return files;
        }
        finally
        {
            fileSystemProvider.DeleteFile(configPath);
        }
    }

    private MongoUrl ResolveMongoUrl()
    {
        var uri = variablesService.GetVariable("BACKUP_MONGO_URI").AsStringWithDefault(_appSettings.ConnectionStrings?.Database);
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new UksfException("No mongo connection string is configured for backups", 500);
        }

        return MongoUrl.Create(uri);
    }

    /// <summary>An empty `BACKUP_MONGO_DATABASES` dumps everything the credentials can see, as one archive.</summary>
    private List<string> ResolveDatabases()
    {
        var configured = variablesService.GetVariable("BACKUP_MONGO_DATABASES").AsStringWithDefault(string.Empty);

        return configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() switch
        {
            { Count: 0 } => [null],
            var list     => list
        };
    }

    private async Task<MongoDumpFile> DumpDatabase(
        MongoUrl mongoUrl,
        string executable,
        string configPath,
        string outputDirectory,
        string database,
        CancellationToken cancellationToken
    )
    {
        var name = database ?? "all";
        var archivePath = Path.Combine(outputDirectory, $"{name}.archive.gz");
        var arguments = BuildArguments(mongoUrl, configPath, archivePath, database);

        var result = await processRunner.Run(executable, outputDirectory, arguments, Timeout, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new UksfException($"mongodump failed for '{name}' with exit code {result.ExitCode}: {Tail(result)}", 500);
        }

        if (!fileSystemProvider.FileExists(archivePath))
        {
            throw new UksfException($"mongodump reported success for '{name}' but wrote no archive: {Tail(result)}", 500);
        }

        var bytes = fileSystemProvider.GetFileSize(archivePath);
        if (bytes == 0)
        {
            throw new UksfException($"mongodump wrote an empty archive for '{name}': {Tail(result)}", 500);
        }

        logger.LogInfo($"Backup dumped mongo '{name}' - {bytes} bytes");
        return new MongoDumpFile
        {
            Database = name,
            Path = archivePath,
            Bytes = bytes
        };
    }

    private static string BuildArguments(MongoUrl mongoUrl, string configPath, string archivePath, string database)
    {
        var arguments = new List<string>
        {
            $"--uri \"{MongoUriCleaner.ForDump(mongoUrl.ToString())}\"",
            $"--config \"{configPath}\"",
            $"--archive=\"{archivePath}\"",
            "--gzip"
        };

        if (!string.IsNullOrWhiteSpace(database))
        {
            arguments.Add($"--db \"{database}\"");
        }

        return string.Join(' ', arguments);
    }

    private static string Tail(ProcessRunResult result)
    {
        var lines = result.Errors.Concat(result.Output).Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(5);
        return string.Join(" | ", lines);
    }
}
