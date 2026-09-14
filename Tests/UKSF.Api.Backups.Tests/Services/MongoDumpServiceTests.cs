using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class MongoDumpServiceTests
{
    private const string Uri = "mongodb://prod:hunter2@localhost:52005/prod?replicaSet=rs0&authSource=admin";

    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IProcessRunner> _mockProcessRunner = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly List<string> _runArguments = [];
    private readonly MongoDumpService _subject;

    public MongoDumpServiceTests()
    {
        _mockVariablesService.Setup(x => x.GetVariable(It.IsAny<string>())).Returns((string key) => new DomainVariableItem { Key = key });

        _mockFileSystemProvider.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemProvider.Setup(x => x.GetFileSize(It.IsAny<string>())).Returns(1024);

        _mockProcessRunner.Setup(x => x.Run(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                          .Callback((string _, string _, string arguments, TimeSpan _, CancellationToken _) => _runArguments.Add(arguments))
                          .ReturnsAsync(new ProcessRunResult { ExitCode = 0 });

        var appSettings = Options.Create(new AppSettings { ConnectionStrings = new AppSettings.ConnectionStringsConfig { Database = Uri } });

        _subject = new MongoDumpService(
            _mockVariablesService.Object,
            appSettings,
            _mockProcessRunner.Object,
            _mockFileSystemProvider.Object,
            _mockLogger.Object
        );
    }

    private void GivenVariable(string key, string value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    [Fact]
    public async Task All_databases_are_dumped_into_one_archive_by_default()
    {
        var result = await _subject.Dump(@"C:\staging");

        result.Should().ContainSingle();
        result[0].Database.Should().Be("all");
        result[0].Path.Should().Be(@"C:\staging\all.archive.gz");
        result[0].Bytes.Should().Be(1024);
        _runArguments.Should().ContainSingle();
        _runArguments[0].Should().NotContain("--db ");
    }

    [Fact]
    public async Task A_configured_database_list_produces_one_archive_each()
    {
        GivenVariable("BACKUP_MONGO_DATABASES", "prod, dev,devLocal");

        var result = await _subject.Dump(@"C:\staging");

        result.Select(x => x.Database).Should().ContainInOrder("prod", "dev", "devLocal");
        _runArguments.Should().HaveCount(3);
        _runArguments[0].Should().Contain("--db \"prod\"").And.Contain(@"--archive=""C:\staging\prod.archive.gz""");
    }

    [Fact]
    public async Task The_credentials_never_reach_the_log()
    {
        await _subject.Dump(@"C:\staging");

        _mockLogger.Verify(x => x.LogInfo(It.Is<string>(y => y.Contains("hunter2"))), Times.Never);
        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(y => y.Contains("hunter2"))), Times.Never);
        _mockLogger.Verify(x => x.LogError(It.Is<string>(y => y.Contains("hunter2"))), Times.Never);
    }

    [Fact]
    public async Task The_password_goes_in_a_config_file_and_never_on_the_command_line()
    {
        await _subject.Dump(@"C:\staging");

        _mockFileSystemProvider.Verify(x => x.WriteAllText(@"C:\staging\mongodump.conf", It.Is<string>(y => y.Contains("password: hunter2"))), Times.Once);
        _runArguments[0].Should().NotContain("hunter2");
        _runArguments[0].Should().Contain(@"--config ""C:\staging\mongodump.conf""");
    }

    [Fact]
    public async Task The_config_file_is_deleted_even_when_the_dump_fails()
    {
        _mockProcessRunner.Setup(x => x.Run(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new ProcessRunResult { ExitCode = 1, Errors = ["auth failed"] });

        var act = () => _subject.Dump(@"C:\staging");

        await act.Should().ThrowAsync<UksfException>();
        _mockFileSystemProvider.Verify(x => x.DeleteFile(@"C:\staging\mongodump.conf"), Times.Once);
    }

    [Fact]
    public async Task The_uri_keeps_the_user_and_host_but_drops_the_database()
    {
        await _subject.Dump(@"C:\staging");

        // the driver normalises the query string order when it rebuilds the uri
        _runArguments[0].Should().Contain("--uri \"mongodb://prod@localhost:52005/?authSource=admin&replicaSet=rs0\"");
        _runArguments[0].Should().Contain("--gzip");
    }

    [Fact]
    public async Task A_backup_specific_uri_overrides_the_api_connection_string()
    {
        GivenVariable("BACKUP_MONGO_URI", "mongodb://backup:s3cret@dedi:52005/?authSource=admin");

        await _subject.Dump(@"C:\staging");

        _runArguments[0].Should().Contain("@dedi:52005").And.NotContain("s3cret");
        _mockFileSystemProvider.Verify(x => x.WriteAllText(It.IsAny<string>(), It.Is<string>(y => y.Contains("password: s3cret"))), Times.Once);
    }

    [Fact]
    public async Task A_custom_mongodump_path_is_used()
    {
        GivenVariable("BACKUP_MONGODUMP_PATH", @"D:\Tools\mongodump.exe");

        await _subject.Dump(@"C:\staging");

        _mockProcessRunner.Verify(
            x => x.Run(@"D:\Tools\mongodump.exe", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task A_non_zero_exit_code_fails_the_dump_with_the_process_output()
    {
        _mockProcessRunner.Setup(x => x.Run(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new ProcessRunResult { ExitCode = 2, Errors = ["Failed: error connecting to db server"] });

        var act = () => _subject.Dump(@"C:\staging");

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("exit code 2").And.Contain("error connecting");
    }

    [Fact]
    public async Task A_success_that_wrote_no_archive_fails()
    {
        _mockFileSystemProvider.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        var act = () => _subject.Dump(@"C:\staging");

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("wrote no archive");
    }

    [Fact]
    public async Task An_empty_archive_fails()
    {
        _mockFileSystemProvider.Setup(x => x.GetFileSize(It.IsAny<string>())).Returns(0);

        var act = () => _subject.Dump(@"C:\staging");

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("empty archive");
    }

    [Fact]
    public async Task No_connection_string_anywhere_fails_before_running_anything()
    {
        var subject = new MongoDumpService(
            _mockVariablesService.Object,
            Options.Create(new AppSettings()),
            _mockProcessRunner.Object,
            _mockFileSystemProvider.Object,
            _mockLogger.Object
        );

        var act = () => subject.Dump(@"C:\staging");

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("No mongo connection string");
        _mockProcessRunner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task The_staging_directory_is_created_before_dumping()
    {
        await _subject.Dump(@"C:\staging");

        _mockFileSystemProvider.Verify(x => x.CreateDirectory(@"C:\staging"), Times.Once);
    }
}
