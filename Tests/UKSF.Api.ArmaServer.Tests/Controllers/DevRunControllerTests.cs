using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class DevRunControllerTests : IDisposable
{
    private readonly Mock<IDevRunService> _service = new();
    private readonly Mock<IDevRunsContext> _context = new();
    private string _tempFile;

    public void Dispose()
    {
        if (_tempFile is not null)
        {
            try
            {
                File.Delete(_tempFile);
            }
            catch { }
        }
    }

    [Fact]
    public void Trigger_returns_202_with_RunId_when_Started()
    {
        _service.Setup(x => x.Trigger("call {}", new[] { "@CBA_A3" }, null)).Returns(new DevRunTriggerResult(DevRunTriggerOutcome.Started, "run-abc"));
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.Trigger(new TriggerDevRunRequest("call {}", new[] { "@CBA_A3" }, null)) as AcceptedResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(202);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"RunId\":\"run-abc\"");
    }

    [Fact]
    public void Trigger_returns_409_with_RunId_when_AlreadyRunning()
    {
        _service.Setup(x => x.Trigger(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<int?>()))
                .Returns(new DevRunTriggerResult(DevRunTriggerOutcome.AlreadyRunning, "run-inflight"));
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.Trigger(new TriggerDevRunRequest("call {}", [], null)) as ConflictObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(409);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"RunId\":\"run-inflight\"");
        json.Should().Contain("\"Error\"");
    }

    [Fact]
    public void Trigger_returns_400_with_MissingPaths_when_BadModPaths()
    {
        var missingPaths = new[] { "/mods/@missing1", "/mods/@missing2" };
        _service.Setup(x => x.Trigger(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<int?>()))
                .Returns(new DevRunTriggerResult(DevRunTriggerOutcome.BadModPaths, null, missingPaths));
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.Trigger(new TriggerDevRunRequest("call {}", [], null)) as BadRequestObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(400);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"Error\"");
        json.Should().Contain("MissingPaths");
    }

    [Fact]
    public void GetStatus_returns_404_when_run_missing()
    {
        _service.Setup(x => x.GetStatus("unknown-run")).Returns((DevRunStatusResponse)null);
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.GetStatus("unknown-run");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetStatus_returns_200_with_status_response()
    {
        var statusResponse = new DevRunStatusResponse("run-abc", DevRunStatus.Running, DateTime.UtcNow, null, null, null);
        _service.Setup(x => x.GetStatus("run-abc")).Returns(statusResponse);
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.GetStatus("run-abc") as OkObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(200);
        result.Value.Should().Be(statusResponse);
    }

    [Fact]
    public void Get_returns_404_when_record_missing()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainDevRun, bool>>())).Returns((DomainDevRun)null);
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.Get("unknown-run");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Get_returns_PhysicalFile_when_ResultFilePath_set_and_exists()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, "result output");
        var record = new DomainDevRun { RunId = "run-abc", ResultFilePath = _tempFile };
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainDevRun, bool>>())).Returns(record);
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.Get("run-abc") as PhysicalFileResult;

        result.Should().NotBeNull();
        result.FileName.Should().Be(_tempFile);
        result.ContentType.Should().Be("text/plain");
        result.FileDownloadName.Should().Be("run-abc.txt");
    }

    [Fact]
    public void Get_returns_Ok_with_record_when_no_ResultFilePath()
    {
        var record = new DomainDevRun
        {
            RunId = "run-abc",
            ResultFilePath = null,
            Result = "some inline result"
        };
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainDevRun, bool>>())).Returns(record);
        var sut = new DevRunController(_service.Object, _context.Object);

        var result = sut.Get("run-abc") as OkObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(200);
        result.Value.Should().Be(record);
    }
}

public class DevRunInternalControllerTests
{
    private readonly Mock<IDevRunService> _service = new();

    [Fact]
    public async Task AppendLog_returns_NoContent_after_calling_service()
    {
        _service.Setup(x => x.AppendLogAsync("run-abc", "log line")).Returns(Task.CompletedTask);
        var sut = new DevRunInternalController(_service.Object);

        var result = await sut.AppendLog("run-abc", new DevRunLogRequest("log line"));

        result.Should().BeOfType<NoContentResult>();
        _service.Verify(x => x.AppendLogAsync("run-abc", "log line"), Times.Once);
    }

    [Fact]
    public async Task AppendResult_reads_raw_body_and_calls_service()
    {
        const string payload = "result payload content";
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        var bodyStream = new MemoryStream(bodyBytes);

        _service.Setup(x => x.AppendResultAsync("run-abc", payload)).Returns(Task.CompletedTask);
        var sut = new DevRunInternalController(_service.Object);
        sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        sut.Request.Body = bodyStream;

        var result = await sut.AppendResult("run-abc");

        result.Should().BeOfType<NoContentResult>();
        _service.Verify(x => x.AppendResultAsync("run-abc", payload), Times.Once);
    }

    [Fact]
    public async Task Finish_returns_NoContent_after_calling_service()
    {
        _service.Setup(x => x.FinishAsync("run-abc")).Returns(Task.CompletedTask);
        var sut = new DevRunInternalController(_service.Object);

        var result = await sut.Finish("run-abc");

        result.Should().BeOfType<NoContentResult>();
        _service.Verify(x => x.FinishAsync("run-abc"), Times.Once);
    }
}
