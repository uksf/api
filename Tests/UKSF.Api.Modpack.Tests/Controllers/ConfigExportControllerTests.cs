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
}
