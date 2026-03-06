using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class RptLogServiceTests : IDisposable
{
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly RptLogService _sut;
    private readonly List<string> _tempDirectories = [];

    public RptLogServiceTests()
    {
        _sut = new RptLogService(_mockVariablesService.Object);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    private static DomainVariableItem CreateVariable(string key, object item)
    {
        return new DomainVariableItem { Key = key, Item = item };
    }

    private void SetupVariable(string key, string value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));
    }

    private static DomainGameServer CreateGameServer(string name = "Main", int numberHeadlessClients = 0)
    {
        return new DomainGameServer
        {
            Name = name,
            NumberHeadlessClients = numberHeadlessClients,
            Environment = GameEnvironment.Release,
            Mods = [],
            ServerMods = []
        };
    }

    private string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"RptLogServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        _tempDirectories.Add(dir);
        return dir;
    }

    #region GetLogSources

    [Fact]
    public void GetLogSources_ReturnsServerOnly_WhenNoHeadlessClients()
    {
        var server = CreateGameServer(numberHeadlessClients: 0);

        var result = _sut.GetLogSources(server);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Server");
        result[0].IsServer.Should().BeTrue();
    }

    [Fact]
    public void GetLogSources_ReturnsServerAndHcs_WhenHeadlessClientsConfigured()
    {
        SetupVariable("SERVER_HEADLESS_NAMES", "Jarvis,Ultron,Vision");
        var server = CreateGameServer(numberHeadlessClients: 2);

        var result = _sut.GetLogSources(server);

        result.Should().HaveCount(3);
        result[0].Should().Be(new RptLogSource("Server", true));
        result[1].Should().Be(new RptLogSource("Jarvis", false));
        result[2].Should().Be(new RptLogSource("Ultron", false));
    }

    [Fact]
    public void GetLogSources_ReturnsCorrectHcNames_ForThreeHeadlessClients()
    {
        SetupVariable("SERVER_HEADLESS_NAMES", "Jarvis,Ultron,Vision");
        var server = CreateGameServer(numberHeadlessClients: 3);

        var result = _sut.GetLogSources(server);

        result.Should().HaveCount(4);
        result[0].Should().Be(new RptLogSource("Server", true));
        result[1].Should().Be(new RptLogSource("Jarvis", false));
        result[2].Should().Be(new RptLogSource("Ultron", false));
        result[3].Should().Be(new RptLogSource("Vision", false));
    }

    #endregion

    #region GetLatestRptFilePath

    [Fact]
    public void GetLatestRptFilePath_ReturnsLatestFile_ForServer()
    {
        var tempDir = CreateTempDirectory();
        var serverProfileDir = Path.Combine(tempDir, "Main");
        Directory.CreateDirectory(serverProfileDir);

        var oldFile = Path.Combine(serverProfileDir, "arma3server_x64_2024-01-15_18-00-00.rpt");
        var newFile = Path.Combine(serverProfileDir, "arma3server_x64_2024-01-15_20-30-00.rpt");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(newFile, "new");
        File.SetLastWriteTime(oldFile, new DateTime(2024, 1, 15, 18, 0, 0));
        File.SetLastWriteTime(newFile, new DateTime(2024, 1, 15, 20, 30, 0));

        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main");

        var result = _sut.GetLatestRptFilePath(server, "Server");

        result.Should().Be(newFile);
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsLatestFile_ForHc()
    {
        var tempDir = CreateTempDirectory();
        var hcProfileDir = Path.Combine(tempDir, "MainJarvis");
        Directory.CreateDirectory(hcProfileDir);

        SetupVariable("SERVER_HEADLESS_NAMES", "Jarvis");

        var oldFile = Path.Combine(hcProfileDir, "arma3server_x64_2024-01-15_18-00-00.rpt");
        var newFile = Path.Combine(hcProfileDir, "arma3server_x64_2024-01-15_20-30-00.rpt");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(newFile, "new");
        File.SetLastWriteTime(oldFile, new DateTime(2024, 1, 15, 18, 0, 0));
        File.SetLastWriteTime(newFile, new DateTime(2024, 1, 15, 20, 30, 0));

        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main", numberHeadlessClients: 1);

        var result = _sut.GetLatestRptFilePath(server, "Jarvis");

        result.Should().Be(newFile);
    }

    [Fact]
    public void GetLatestRptFilePath_SortsByModificationTime_NotFilename()
    {
        var tempDir = CreateTempDirectory();
        var serverProfileDir = Path.Combine(tempDir, "Main");
        Directory.CreateDirectory(serverProfileDir);

        var fileA = Path.Combine(serverProfileDir, "aaa.rpt");
        var fileZ = Path.Combine(serverProfileDir, "zzz.rpt");
        File.WriteAllText(fileA, "content");
        File.WriteAllText(fileZ, "content");
        File.SetLastWriteTime(fileA, new DateTime(2024, 6, 1));
        File.SetLastWriteTime(fileZ, new DateTime(2024, 1, 1));

        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main");

        var result = _sut.GetLatestRptFilePath(server, "Server");

        result.Should().Be(fileA, "should pick the file with the latest modification time, not the last alphabetically");
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsNull_WhenNoFilesExist()
    {
        var tempDir = CreateTempDirectory();
        var serverProfileDir = Path.Combine(tempDir, "Main");
        Directory.CreateDirectory(serverProfileDir);

        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main");

        var result = _sut.GetLatestRptFilePath(server, "Server");

        result.Should().BeNull();
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsNull_WhenDirectoryNotFound()
    {
        var tempDir = CreateTempDirectory();
        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "NonExistent");

        var result = _sut.GetLatestRptFilePath(server, "Server");

        result.Should().BeNull();
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsNull_WhenSourceIsInvalid()
    {
        var tempDir = CreateTempDirectory();
        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main");

        var result = _sut.GetLatestRptFilePath(server, "../../etc");

        result.Should().BeNull();
    }

    [Fact]
    public void GetLatestRptFilePath_ReturnsNull_WhenSourceNotInConfiguredHcs()
    {
        SetupVariable("SERVER_HEADLESS_NAMES", "Jarvis");
        var tempDir = CreateTempDirectory();
        SetupVariable("SERVER_PATH_PROFILES", tempDir);
        var server = CreateGameServer(name: "Main", numberHeadlessClients: 1);

        var result = _sut.GetLatestRptFilePath(server, "Ultron");

        result.Should().BeNull();
    }

    #endregion

    #region ReadFullFile

    [Fact]
    public void ReadFullFile_ReturnsAllLines()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        var lines = Enumerable.Range(1, 10).Select(i => $"Line {i}").ToList();
        File.WriteAllLines(filePath, lines);

        var (result, bytesRead) = _sut.ReadFullFile(filePath);

        result.Should().HaveCount(10);
        result.Should().BeEquivalentTo(lines);
        bytesRead.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReadFullFile_HandlesEmptyFile()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "empty.rpt");
        File.WriteAllText(filePath, "");

        var (result, bytesRead) = _sut.ReadFullFile(filePath);

        result.Should().BeEmpty();
        bytesRead.Should().Be(0);
    }

    [Fact]
    public void ReadFullFile_HandlesLargeFile()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "large.rpt");
        var lines = Enumerable.Range(1, 50000).Select(i => $"Line {i}").ToList();
        File.WriteAllLines(filePath, lines);

        var (result, _) = _sut.ReadFullFile(filePath);

        result.Should().HaveCount(50000);
        result.First().Should().Be("Line 1");
        result.Last().Should().Be("Line 50000");
    }

    [Fact]
    public void ReadFullFile_ReadsFile_WhenLockedByAnotherProcess()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "locked.rpt");
        File.WriteAllLines(filePath, ["Line 1", "Line 2", "Line 3"]);

        using var lockingStream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        var (result, _) = _sut.ReadFullFile(filePath);

        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(["Line 1", "Line 2", "Line 3"]);
    }

    [Fact]
    public void ReadFullFile_BytesRead_MatchesFileLength()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllText(filePath, "Line 1\nLine 2\nLine 3\n");
        var expectedLength = new FileInfo(filePath).Length;

        var (_, bytesRead) = _sut.ReadFullFile(filePath);

        bytesRead.Should().Be(expectedLength);
    }

    #endregion

    #region SearchFile

    [Fact]
    public void SearchFile_FindsMatchingLines()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllLines(filePath, ["First line", "Error: something failed", "Normal line", "Error: another failure"]);

        var result = _sut.SearchFile(filePath, "Error:");

        result.Results.Should().HaveCount(2);
        result.Results[0].Should().Be(new RptLogSearchResult(1, "Error: something failed"));
        result.Results[1].Should().Be(new RptLogSearchResult(3, "Error: another failure"));
        result.TotalMatches.Should().Be(2);
    }

    [Fact]
    public void SearchFile_IsCaseInsensitive()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllLines(filePath, ["WARNING: loud message", "info: quiet message", "Warning: mixed case"]);

        var result = _sut.SearchFile(filePath, "warning");

        result.Results.Should().HaveCount(2);
        result.Results[0].LineIndex.Should().Be(0);
        result.Results[1].LineIndex.Should().Be(2);
        result.TotalMatches.Should().Be(2);
    }

    [Fact]
    public void SearchFile_ReturnsEmpty_WhenNoMatches()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllLines(filePath, ["Line one", "Line two", "Line three"]);

        var result = _sut.SearchFile(filePath, "nonexistent");

        result.Results.Should().BeEmpty();
        result.TotalMatches.Should().Be(0);
    }

    [Fact]
    public void SearchFile_HandlesRegexSpecialCharacters()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllLines(filePath, ["[ACE] Medical initialized", "Normal line", "[ACE] Logistics loaded"]);

        var result = _sut.SearchFile(filePath, "[ACE]");

        result.Results.Should().HaveCount(2);
        result.Results[0].Should().Be(new RptLogSearchResult(0, "[ACE] Medical initialized"));
        result.Results[1].Should().Be(new RptLogSearchResult(2, "[ACE] Logistics loaded"));
        result.TotalMatches.Should().Be(2);
    }

    [Fact]
    public void SearchFile_ReadsFile_WhenLockedByAnotherProcess()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "locked.rpt");
        File.WriteAllLines(filePath, ["First line", "Error: something failed", "Normal line"]);

        using var lockingStream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        var result = _sut.SearchFile(filePath, "Error:");

        result.Results.Should().HaveCount(1);
        result.Results[0].Should().Be(new RptLogSearchResult(1, "Error: something failed"));
        result.TotalMatches.Should().Be(1);
    }

    [Fact]
    public void SearchFile_CountsMultipleMatchesPerLine()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllLines(filePath, ["error error error", "no match", "error here"]);

        var result = _sut.SearchFile(filePath, "error");

        result.Results.Should().HaveCount(2);
        result.TotalMatches.Should().Be(4);
    }

    #endregion

    #region WatchFile

    [Fact]
    public async Task WatchFile_CallsBackWithNewContent_WhenFileAppended()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllText(filePath, "Initial line\n");

        var receivedLines = new ConcurrentBag<List<string>>();
        var tcs = new TaskCompletionSource<bool>();
        var startOffset = new FileInfo(filePath).Length;

        using var watcher = _sut.WatchFile(
            filePath,
            startOffset,
            lines =>
            {
                receivedLines.Add(lines);
                tcs.TrySetResult(true);
            }
        );

        await Task.Delay(500);
        File.AppendAllText(filePath, "New line 1\nNew line 2\n");

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(tcs.Task, "callback should be invoked within 5 seconds");

        var allLines = receivedLines.SelectMany(l => l).ToList();
        allLines.Should().Contain("New line 1");
        allLines.Should().Contain("New line 2");
    }

    [Fact]
    public async Task WatchFile_DoesNotCallBack_WhenNoNewContent()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllText(filePath, "Initial line\n");

        var callbackInvoked = false;
        var startOffset = new FileInfo(filePath).Length;

        using var watcher = _sut.WatchFile(filePath, startOffset, _ => { callbackInvoked = true; });

        await Task.Delay(TimeSpan.FromSeconds(3));

        callbackInvoked.Should().BeFalse("callback should not be invoked when file has no new content");
    }

    [Fact]
    public async Task WatchFile_StopsWatching_WhenDisposed()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllText(filePath, "Initial line\n");

        var callbackInvokedAfterDispose = false;
        var startOffset = new FileInfo(filePath).Length;

        var watcher = _sut.WatchFile(filePath, startOffset, _ => { callbackInvokedAfterDispose = true; });

        await Task.Delay(500);
        watcher.Dispose();
        await Task.Delay(500);

        File.AppendAllText(filePath, "New content after dispose\n");

        await Task.Delay(TimeSpan.FromSeconds(3));

        callbackInvokedAfterDispose.Should().BeFalse("callback should not be invoked after watcher is disposed");
    }

    [Fact]
    public async Task WatchFile_TracksOffset_AcrossMultipleAppends()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "test.rpt");
        File.WriteAllText(filePath, "Initial line\n");

        var receivedBatches = new ConcurrentBag<List<string>>();
        var firstBatchTcs = new TaskCompletionSource<bool>();
        var secondBatchTcs = new TaskCompletionSource<bool>();
        var batchCount = 0;
        var startOffset = new FileInfo(filePath).Length;

        using var watcher = _sut.WatchFile(
            filePath,
            startOffset,
            lines =>
            {
                receivedBatches.Add(lines);
                var count = System.Threading.Interlocked.Increment(ref batchCount);
                if (count == 1)
                {
                    firstBatchTcs.TrySetResult(true);
                }
                else if (count >= 2)
                {
                    secondBatchTcs.TrySetResult(true);
                }
            }
        );

        await Task.Delay(500);
        File.AppendAllText(filePath, "First append\n");

        var firstCompleted = await Task.WhenAny(firstBatchTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        firstCompleted.Should().Be(firstBatchTcs.Task, "first callback should be invoked within 5 seconds");

        await Task.Delay(500);
        File.AppendAllText(filePath, "Second append\n");

        var secondCompleted = await Task.WhenAny(secondBatchTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        secondCompleted.Should().Be(secondBatchTcs.Task, "second callback should be invoked within 5 seconds");

        var allLines = receivedBatches.SelectMany(l => l).ToList();
        allLines.Should().Contain("First append");
        allLines.Should().Contain("Second append");
        allLines.Should().NotContain("Initial line");

        var duplicates = allLines.GroupBy(l => l).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        duplicates.Should().BeEmpty("no lines should be received more than once");
    }

    #endregion
}
