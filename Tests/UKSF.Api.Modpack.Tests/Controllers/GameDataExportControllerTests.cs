using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Controllers;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Controllers;

public class GameDataExportControllerTests : IDisposable
{
    private readonly Mock<IGameDataExportsContext> _context = new();
    private readonly Mock<IGameDataExportService> _service = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly GameDataExportController _controller;
    private readonly string _tempConfig;
    private readonly string _tempSettings;

    public GameDataExportControllerTests()
    {
        _tempConfig = Path.Combine(Path.GetTempPath(), "uksf-gamedata-config-" + Guid.NewGuid());
        _tempSettings = Path.Combine(Path.GetTempPath(), "uksf-gamedata-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempConfig);
        Directory.CreateDirectory(_tempSettings);
        _controller = new GameDataExportController(_service.Object, _context.Object, _variablesService.Object);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempConfig, true);
        }
        catch { }

        try
        {
            Directory.Delete(_tempSettings, true);
        }
        catch { }
    }

    private void SetupVariable(string key, string value)
    {
        _variablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    [Fact]
    public void Trigger_ReturnsAcceptedWithRunId()
    {
        _service.Setup(x => x.Trigger("5.23.9")).Returns(new TriggerResult(TriggerOutcome.Started, "abc-123"));

        var result = _controller.Trigger(new TriggerGameDataExportRequest("5.23.9")) as AcceptedResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(202);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"RunId\":\"abc-123\"", "result body should include the run id");
    }

    [Fact]
    public void Trigger_WhenAlreadyRunning_Returns409()
    {
        _service.Setup(x => x.Trigger(It.IsAny<string>())).Returns(new TriggerResult(TriggerOutcome.AlreadyRunning, "in-flight"));

        var result = _controller.Trigger(new TriggerGameDataExportRequest("5.23.9")) as ConflictObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(409);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"RunId\":\"in-flight\"");
        json.Should().Contain("\"Error\"");
    }

    [Fact]
    public void GetStatus_DelegatesToService()
    {
        var statusResponse = new GameDataExportStatusResponse("abc-123", GameDataExportStatus.Running, DateTime.UtcNow);
        _service.Setup(x => x.GetStatus()).Returns(statusResponse);

        var result = _controller.GetStatus();

        result.Should().BeEquivalentTo(statusResponse);
    }

    [Theory]
    [InlineData("config")]
    [InlineData("cba-settings")]
    [InlineData("cba-settings-reference")]
    public void Download_Valid_File_Param_Returns_File_When_Present(string file)
    {
        var records = new[]
        {
            new DomainGameDataExport
            {
                ModpackVersion = "5.23.9",
                Status = GameDataExportStatus.Success,
                CompletedAt = DateTime.UtcNow,
                HasConfig = true,
                HasCbaSettings = true,
                HasCbaSettingsReference = true
            }
        };
        _context.Setup(c => c.Get(It.IsAny<Func<DomainGameDataExport, bool>>()))
                .Returns((Func<DomainGameDataExport, bool> predicate) => records.Where(predicate));
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", _tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", _tempSettings);

        File.WriteAllText(Path.Combine(_tempConfig, "config_5.23.9.cpp"), "x");
        File.WriteAllText(Path.Combine(_tempSettings, "cba_settings_5.23.9.sqf"), "x");
        File.WriteAllText(Path.Combine(_tempSettings, "cba_settings_reference_5.23.9.json"), "x");

        var result = _controller.Download("5.23.9", file);

        result.Should().BeOfType<PhysicalFileResult>();
    }

    [Fact]
    public void Download_Invalid_File_Param_Returns_NotFound()
    {
        var result = _controller.Download("5.23.9", "definitely-not-a-real-file");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Download_Missing_Presence_Flag_Returns_NotFound()
    {
        var records = new[]
        {
            new DomainGameDataExport
            {
                ModpackVersion = "5.23.9",
                Status = GameDataExportStatus.PartialSuccess,
                CompletedAt = DateTime.UtcNow,
                HasConfig = true,
                HasCbaSettings = false,
                HasCbaSettingsReference = false
            }
        };
        _context.Setup(c => c.Get(It.IsAny<Func<DomainGameDataExport, bool>>()))
                .Returns((Func<DomainGameDataExport, bool> predicate) => records.Where(predicate));
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", _tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", _tempSettings);
        File.WriteAllText(Path.Combine(_tempConfig, "config_5.23.9.cpp"), "x");

        _controller.Download("5.23.9", "cba-settings").Should().BeOfType<NotFoundResult>();
        _controller.Download("5.23.9", "cba-settings-reference").Should().BeOfType<NotFoundResult>();
        _controller.Download("5.23.9", "config").Should().NotBeOfType<NotFoundResult>();
    }

    [Fact]
    public void List_Returns_All_Docs_With_Presence_Flags()
    {
        var docs = new[]
        {
            new DomainGameDataExport
            {
                ModpackVersion = "5.23.9",
                HasConfig = true,
                HasCbaSettings = true,
                HasCbaSettingsReference = true
            },
            new DomainGameDataExport
            {
                ModpackVersion = "5.23.8",
                HasConfig = true,
                HasCbaSettings = false,
                HasCbaSettingsReference = false
            }
        };
        _context.Setup(c => c.Get()).Returns(docs);

        var result = _controller.List().ToList();

        result.Should().HaveCount(2);
        result.First(d => d.ModpackVersion == "5.23.8").HasCbaSettings.Should().BeFalse();
    }
}
