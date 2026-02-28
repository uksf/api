using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

[Collection("MissionPatchData")]
public class MissionServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MissionService _service;
    private readonly Mock<ISqmDecompiler> _mockDecompiler = new();
    private readonly Mock<IRanksContext> _mockRanksContext = new();
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IRanksService> _mockRanksService = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();

    public MissionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_mission_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _mockDecompiler.Setup(x => x.IsBinarized(It.IsAny<string>())).ReturnsAsync(false);

        var patchDataService = new MissionPatchDataService(
            _mockRanksContext.Object,
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockRanksService.Object,
            _mockDisplayNameService.Object
        );
        _service = new MissionService(patchDataService, _mockDecompiler.Object);
    }

    public void Dispose()
    {
        MissionPatchData.Instance = null;
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task ProcessMission_ShouldReportError_WhenDescriptionExtMissing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "");
        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, "", 5);

        reports.Should().HaveCount(1);
        reports[0].Error.Should().BeTrue();
        reports[0].Title.Should().Contain("description.ext");
    }

    [Fact]
    public async Task ProcessMission_ShouldReportError_WhenCbaSettingsMissing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "maxPlayers = 10;");
        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, "", 5);

        reports.Should().HaveCount(1);
        reports[0].Error.Should().BeTrue();
        reports[0].Title.Should().Contain("cba_settings.sqf");
    }

    [Fact]
    public async Task ProcessMission_ShouldDeleteReadmeTxt_WhenPresent()
    {
        CreateMinimalMissionFiles();
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var readmePath = Path.Combine(_tempDir, "README.txt");
        File.WriteAllText(readmePath, "readme content");
        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        File.Exists(readmePath).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessMission_ShouldStopAtFirstMissingFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "maxPlayers = 10;");
        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, "", 5);

        reports.Should().HaveCount(1);
        reports[0].Error.Should().BeTrue();
        reports[0].Title.Should().Contain("cba_settings.sqf");
    }

    [Fact]
    public async Task ProcessMission_ShouldCallDecompiler_WhenSqmIsBinarized()
    {
        CreateMinimalMissionFiles();
        CreateMinimalSqm();
        SetupPatchDataMocks();

        _mockDecompiler.Setup(x => x.IsBinarized(It.IsAny<string>())).ReturnsAsync(true);
        _mockDecompiler.Setup(x => x.Decompile(It.IsAny<string>())).Returns(Task.CompletedTask);

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        _mockDecompiler.Verify(x => x.Decompile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMission_ShouldNotCallDecompiler_WhenSqmIsNotBinarized()
    {
        CreateMinimalMissionFiles();
        CreateMinimalSqm();
        SetupPatchDataMocks();

        _mockDecompiler.Setup(x => x.IsBinarized(It.IsAny<string>())).ReturnsAsync(false);

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        _mockDecompiler.Verify(x => x.Decompile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMission_ShouldSkipPatching_WhenPatchingIgnoreFlagSet()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "missionPatchingIgnore = 1;\nmaxPlayers = 10;");
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();
        // Don't set up patch data mocks - patching should be skipped

        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, _tempDir, 5);

        reports.Should().Contain(r => r.Title.Contains("Mission Patching Ignored"));
    }

    [Fact]
    public async Task ProcessMission_ShouldStillPatchDescription_WhenPatchingIgnored()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "missionPatchingIgnore = 1;\nmaxPlayers = 10;");
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        // Description should still be patched (required items added)
        var descriptionLines = File.ReadAllLines(Path.Combine(_tempDir, "description.ext"));
        descriptionLines.Should().Contain(x => x.Contains("author"));
    }

    [Fact]
    public async Task ProcessMission_ShouldReadCuratorsMax_FromCbaSettings()
    {
        CreateMinimalMissionFiles("uksf_curator_curatorsMax = 7;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        mission.MaxCurators.Should().Be(7);
    }

    [Fact]
    public async Task ProcessMission_ShouldUseServerDefault_WhenCuratorsMaxMissing()
    {
        CreateMinimalMissionFiles("// no curator setting");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, _tempDir, 8);

        mission.MaxCurators.Should().Be(8);
        reports.Should().Contain(r => r.Title.Contains("Using server setting"));
    }

    [Fact]
    public async Task ProcessMission_ShouldUseHardcodedDefault_WhenCuratorsMaxMalformed()
    {
        CreateMinimalMissionFiles("uksf_curator_curatorsMax = abc;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, _tempDir, 8);

        mission.MaxCurators.Should().Be(5);
        reports.Should().Contain(r => r.Title.Contains("Using hardcoded setting"));
    }

    [Fact]
    public async Task ProcessMission_ShouldPatchMaxPlayers_InDescription()
    {
        CreateMinimalMissionFiles();
        CreateSqmWithPlayables(3);
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        var descriptionLines = File.ReadAllLines(Path.Combine(_tempDir, "description.ext"));
        descriptionLines.Should().Contain(x => x.Contains("maxPlayers") && x.Contains("3"));
    }

    [Fact]
    public async Task ProcessMission_ShouldReportError_WhenMaxPlayersMissing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "author = \"UKSF\";");
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, _tempDir, 5);

        reports.Should().Contain(r => r.Error && r.Title.Contains("maxPlayers"));
    }

    [Fact]
    public async Task ProcessMission_ShouldAddMissingRequiredDescriptionItems()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "maxPlayers = 10;");
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        var descriptionLines = File.ReadAllLines(Path.Combine(_tempDir, "description.ext"));
        descriptionLines.Should().Contain(x => x.Contains("author") && x.Contains("\"UKSF\""));
        descriptionLines.Should().Contain(x => x.Contains("loadScreen") && x.Contains("\"uksf.paa\""));
        descriptionLines.Should().Contain(x => x.Contains("respawn") && x.Contains("\"BASE\""));
        descriptionLines.Should().Contain(x => x.Contains("disabledAI") && x.Contains("1"));
    }

    [Fact]
    public async Task ProcessMission_ShouldReportWarning_WhenRequiredItemHasNonDefaultValue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "maxPlayers = 10;\nauthor = \"Custom Author\";");
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, _tempDir, 5);

        reports.Should().Contain(r => !r.Error && r.Title.Contains("author") && r.Title.Contains("not default"));
    }

    [Fact]
    public async Task ProcessMission_ShouldReportWarning_WhenConfigurableItemIsDefault()
    {
        var descContent = "maxPlayers = 10;\nonLoadName = \"UKSF: Operation\";";
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), descContent);
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, _tempDir, 5);

        reports.Should().Contain(r => !r.Error && r.Title.Contains("onLoadName") && r.Title.Contains("default"));
    }

    [Fact]
    public async Task ProcessMission_ShouldReportError_WhenConfigurableItemMissing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "maxPlayers = 10;");
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        var reports = await _service.ProcessMission(mission, _tempDir, 5);

        reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadName") && r.Title.Contains("missing"));
    }

    [Fact]
    public async Task ProcessMission_ShouldRemoveExecLines_FromDescription()
    {
        var descContent = "maxPlayers = 10;\n__EXEC(something);\nauthor = \"UKSF\";";
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), descContent);
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
        CreateMinimalSqm();
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        var descriptionLines = File.ReadAllLines(Path.Combine(_tempDir, "description.ext"));
        descriptionLines.Should().NotContain(x => x.Contains("__EXEC"));
    }

    [Fact]
    public async Task ProcessMission_ShouldCountPlayableEntities()
    {
        CreateMinimalMissionFiles();
        CreateSqmWithPlayables(5);
        SetupPatchDataMocks();

        var mission = new Mission(_tempDir);

        await _service.ProcessMission(mission, _tempDir, 5);

        mission.PlayerCount.Should().Be(5);
    }

    private void CreateMinimalMissionFiles(string cbaContent = "uksf_curator_curatorsMax = 5;")
    {
        File.WriteAllText(Path.Combine(_tempDir, "description.ext"), "maxPlayers = 10;");
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), cbaContent);
    }

    private void CreateMinimalSqm()
    {
        var lines = new List<string>
        {
            "version=54;",
            "class EditorData",
            "{",
            "};",
            "class ItemIDProvider",
            "{",
            "nextID=10;",
            "};",
            "class Mission",
            "{",
            "class Entities",
            "{",
            "items = 0;",
            "};",
            "};"
        };
        File.WriteAllLines(Path.Combine(_tempDir, "mission.sqm"), lines);
    }

    private void CreateSqmWithPlayables(int count)
    {
        var lines = new List<string>
        {
            "version=54;",
            "class ItemIDProvider",
            "{",
            $"nextID={count + 10};",
            "};",
            "class Mission",
            "{",
            "class Entities",
            "{",
            $"items = {count};",
        };

        for (var i = 0; i < count; i++)
        {
            lines.AddRange(
                [
                    $"class Item{i}",
                    "{",
                    "dataType=\"Object\";",
                    $"id={i + 10};",
                    "isPlayable=1;",
                    "side=\"West\";",
                    "type=\"UKSF_B_Rifleman\";",
                    "};"
                ]
            );
        }

        lines.AddRange(["};", "};"]);
        File.WriteAllLines(Path.Combine(_tempDir, "mission.sqm"), lines);
    }

    private void SetupPatchDataMocks()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var rootUnit = new DomainUnit
        {
            Id = parentId,
            Name = "Root",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Root"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { rootUnit });
        _mockRanksContext.Setup(x => x.Get()).Returns(new List<DomainRank>());
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount>());
    }
}
