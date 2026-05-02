using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Modpack.Controllers;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Controllers;

public class ConfigExportControllerTests
{
    private readonly Mock<IGameConfigExportsContext> _context = new();
    private readonly Mock<IConfigExportService> _service = new();

    [Fact]
    public void Trigger_ReturnsAcceptedWithRunId()
    {
        _service.Setup(x => x.Trigger("5.23.9")).Returns(new TriggerResult(TriggerOutcome.Started, "abc-123"));
        var sut = new ConfigExportController(_service.Object, _context.Object);

        var result = sut.Trigger(new TriggerConfigExportRequest("5.23.9")) as AcceptedResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(202);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"RunId\":\"abc-123\"", "result body should include the run id");
    }

    [Fact]
    public void Trigger_WhenAlreadyRunning_Returns409()
    {
        _service.Setup(x => x.Trigger(It.IsAny<string>())).Returns(new TriggerResult(TriggerOutcome.AlreadyRunning, "in-flight"));
        var sut = new ConfigExportController(_service.Object, _context.Object);

        var result = sut.Trigger(new TriggerConfigExportRequest("5.23.9")) as ConflictObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(409);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"RunId\":\"in-flight\"");
        json.Should().Contain("\"Error\"");
    }

    [Fact]
    public void GetStatus_DelegatesToService()
    {
        var statusResponse = new ConfigExportStatusResponse("abc-123", ConfigExportStatus.Running, DateTime.UtcNow);
        _service.Setup(x => x.GetStatus()).Returns(statusResponse);
        var sut = new ConfigExportController(_service.Object, _context.Object);

        var result = sut.GetStatus();

        result.Should().BeEquivalentTo(statusResponse);
    }

    [Fact]
    public void GetExport_ReturnsNotFound_WhenRecordMissing()
    {
        _context.Setup(x => x.GetSingle("unknown-id")).Returns((DomainGameConfigExport)null);
        var sut = new ConfigExportController(_service.Object, _context.Object);

        var result = sut.GetExport("unknown-id");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetExport_ReturnsNotFound_WhenFileMissing()
    {
        _context.Setup(x => x.GetSingle("abc")).Returns(new DomainGameConfigExport { Id = "abc", FilePath = "C:/does/not/exist.cpp" });
        var sut = new ConfigExportController(_service.Object, _context.Object);

        var result = sut.GetExport("abc");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetExport_ReturnsPhysicalFile_WhenAvailable()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "hello");
            _context.Setup(x => x.GetSingle("abc")).Returns(new DomainGameConfigExport { Id = "abc", FilePath = tempFile });
            var sut = new ConfigExportController(_service.Object, _context.Object);

            var result = sut.GetExport("abc") as PhysicalFileResult;

            result.Should().NotBeNull();
            result.FileDownloadName.Should().Be(Path.GetFileName(tempFile));
            result.ContentType.Should().Be("text/plain");
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch { }
        }
    }

    [Fact]
    public void DownloadByVersion_ReturnsNotFound_WhenNoSuccessfulExport()
    {
        _context.Setup(x => x.Get(It.IsAny<Func<DomainGameConfigExport, bool>>()))
                .Returns((Func<DomainGameConfigExport, bool> predicate) => Array.Empty<DomainGameConfigExport>().Where(predicate));
        var sut = new ConfigExportController(_service.Object, _context.Object);

        var result = sut.DownloadByVersion("5.23.9");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void DownloadByVersion_ReturnsNotFound_WhenFileMissingOnDisk()
    {
        var records = new[]
        {
            new DomainGameConfigExport
            {
                ModpackVersion = "5.23.9",
                Status = ConfigExportStatus.Success,
                CompletedAt = DateTime.UtcNow,
                FilePath = "C:/does/not/exist.cpp"
            }
        };
        _context.Setup(x => x.Get(It.IsAny<Func<DomainGameConfigExport, bool>>()))
                .Returns((Func<DomainGameConfigExport, bool> predicate) => records.Where(predicate));
        var sut = new ConfigExportController(_service.Object, _context.Object);

        var result = sut.DownloadByVersion("5.23.9");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void DownloadByVersion_ReturnsLatestSuccessfulExport_WhenMultipleExist()
    {
        var older = Path.GetTempFileName();
        var newer = Path.GetTempFileName();
        try
        {
            File.WriteAllText(older, "old");
            File.WriteAllText(newer, "new");
            var records = new[]
            {
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.9",
                    Status = ConfigExportStatus.Success,
                    CompletedAt = DateTime.UtcNow.AddHours(-1),
                    FilePath = older
                },
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.9",
                    Status = ConfigExportStatus.Success,
                    CompletedAt = DateTime.UtcNow,
                    FilePath = newer
                },
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.9",
                    Status = ConfigExportStatus.FailedTimeout,
                    CompletedAt = DateTime.UtcNow.AddMinutes(1),
                    FilePath = older
                }
            };
            _context.Setup(x => x.Get(It.IsAny<Func<DomainGameConfigExport, bool>>()))
                    .Returns((Func<DomainGameConfigExport, bool> predicate) => records.Where(predicate));
            var sut = new ConfigExportController(_service.Object, _context.Object);

            var result = sut.DownloadByVersion("5.23.9") as PhysicalFileResult;

            result.Should().NotBeNull();
            result.FileName.Should().Be(newer);
            result.FileDownloadName.Should().Be("config_5.23.9.cpp");
            result.ContentType.Should().Be("text/plain");
        }
        finally
        {
            try
            {
                File.Delete(older);
            }
            catch { }

            try
            {
                File.Delete(newer);
            }
            catch { }
        }
    }

    [Fact]
    public void GetAvailableVersions_ReturnsDistinctVersions_WithExistingFiles()
    {
        var existing = Path.GetTempFileName();
        try
        {
            File.WriteAllText(existing, "x");
            var records = new[]
            {
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.9",
                    Status = ConfigExportStatus.Success,
                    FilePath = existing
                },
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.9",
                    Status = ConfigExportStatus.Success,
                    FilePath = existing
                },
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.8",
                    Status = ConfigExportStatus.Success,
                    FilePath = "C:/missing.cpp"
                },
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.7",
                    Status = ConfigExportStatus.FailedTimeout,
                    FilePath = existing
                },
                new DomainGameConfigExport
                {
                    ModpackVersion = "5.23.6",
                    Status = ConfigExportStatus.Success,
                    FilePath = null
                }
            };
            _context.Setup(x => x.Get(It.IsAny<Func<DomainGameConfigExport, bool>>()))
                    .Returns((Func<DomainGameConfigExport, bool> predicate) => records.Where(predicate));
            var sut = new ConfigExportController(_service.Object, _context.Object);

            var result = sut.GetAvailableVersions();

            result.Should().BeEquivalentTo(new[] { "5.23.9" });
        }
        finally
        {
            try
            {
                File.Delete(existing);
            }
            catch { }
        }
    }
}
