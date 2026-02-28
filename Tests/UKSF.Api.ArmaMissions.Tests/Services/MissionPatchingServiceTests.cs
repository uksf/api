using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

[Collection("MissionPatchData")]
public class MissionPatchingServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _backupDir;
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IPboTools> _mockPboTools = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<ISqmDecompiler> _mockDecompiler = new();
    private readonly MissionPatchingService _service;
    private readonly MissionService _missionService;

    public MissionPatchingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_patch_test_{Guid.NewGuid():N}");
        _backupDir = Path.Combine(Path.GetTempPath(), $"uksf_patch_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_backupDir);

        _mockVariablesService.Setup(x => x.GetVariable("MISSIONS_BACKUPS")).Returns(new DomainVariableItem { Key = "MISSIONS_BACKUPS", Item = _backupDir });
        _mockVariablesService.Setup(x => x.GetVariable("MISSIONS_WORKING_DIR"))
                             .Returns(new DomainVariableItem { Key = "MISSIONS_WORKING_DIR", Item = _tempDir });

        _mockDecompiler.Setup(x => x.IsBinarized(It.IsAny<string>())).ReturnsAsync(false);

        var patchDataService = new MissionPatchDataService(
            new Mock<IRanksContext>().Object,
            new Mock<IAccountContext>().Object,
            CreateMockUnitsContext(),
            new Mock<IRanksService>().Object,
            new Mock<IDisplayNameService>().Object
        );
        _missionService = new MissionService(patchDataService, _mockDecompiler.Object);
        _service = new MissionPatchingService(_missionService, _mockVariablesService.Object, _mockPboTools.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        MissionPatchData.Instance = null;
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        if (Directory.Exists(_backupDir)) Directory.Delete(_backupDir, true);
    }

    [Fact]
    public async Task PatchMission_ShouldCreateBackup()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath);

        await _service.PatchMission(pboPath, _tempDir, 5);

        var backupPath = Path.Combine(_backupDir, Path.GetFileName(pboPath));
        File.Exists(backupPath).Should().BeTrue();
    }

    [Fact]
    public async Task PatchMission_ShouldStripQuotesFromPath()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath);

        var result = await _service.PatchMission($"\"{pboPath}\"", _tempDir, 5);

        // Should not throw from quotes in path
        var backupPath = Path.Combine(_backupDir, Path.GetFileName(pboPath));
        File.Exists(backupPath).Should().BeTrue();
    }

    [Fact]
    public async Task PatchMission_ShouldRejectNonPboFiles()
    {
        var txtPath = Path.Combine(_tempDir, "mission.txt");
        File.WriteAllText(txtPath, "not a pbo");

        // Non-pbo throws FileLoadException which is caught → failure result
        // Cleanup will try to delete _folderPath which is null, but the catch handles it
        var result = await _service.PatchMission(txtPath, _tempDir, 5);

        result.Success.Should().BeFalse();
        result.Reports.Should().NotBeEmpty();
        result.Reports[0].Error.Should().BeTrue();
    }

    [Fact]
    public async Task PatchMission_ShouldCallExtractPbo()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath);

        await _service.PatchMission(pboPath, _tempDir, 5);

        _mockPboTools.Verify(x => x.ExtractPbo(pboPath, _tempDir), Times.Once);
    }

    [Fact]
    public async Task PatchMission_ShouldCallMakePbo_WhenNoSimplePackFlag()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath, useSimplePack: false, includeAllDescriptionItems: true);

        await _service.PatchMission(pboPath, _tempDir, 5);

        _mockPboTools.Verify(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockPboTools.Verify(x => x.SimplePackPbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchMission_ShouldCallSimplePackPbo_WhenSimplePackFlagSet()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath, useSimplePack: true, includeAllDescriptionItems: true);

        await _service.PatchMission(pboPath, _tempDir, 5);

        _mockPboTools.Verify(x => x.SimplePackPbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockPboTools.Verify(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchMission_ShouldLogAudit_WhenSimplePackUsed()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath, useSimplePack: true, includeAllDescriptionItems: true);

        await _service.PatchMission(pboPath, _tempDir, 5);

        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("simple packing"))), Times.Once);
    }

    [Fact]
    public async Task PatchMission_ShouldReturnSuccess_WhenNoErrorReports()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath, includeAllDescriptionItems: true);

        var result = await _service.PatchMission(pboPath, _tempDir, 5);

        // Success means no error-level reports (warnings are OK)
        result.Success.Should().BeTrue();
        result.Reports.Should().OnlyContain(r => !r.Error);
    }

    [Fact]
    public async Task PatchMission_ShouldReturnFailure_WhenProcessingReportsErrors()
    {
        var pboPath = CreateTestPbo();
        // Create mission folder without description.ext so processing fails
        SetupExtractToCreateMissionFolder(pboPath, includeDescriptionExt: false);

        var result = await _service.PatchMission(pboPath, _tempDir, 5);

        result.Success.Should().BeFalse();
        result.Reports.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PatchMission_ShouldNotCallPacking_WhenProcessingFails()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath, includeDescriptionExt: false);

        await _service.PatchMission(pboPath, _tempDir, 5);

        _mockPboTools.Verify(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockPboTools.Verify(x => x.SimplePackPbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchMission_ShouldReturnPlayerCount()
    {
        var pboPath = CreateTestPbo();
        SetupExtractToCreateMissionFolder(pboPath, playableCount: 3);

        var result = await _service.PatchMission(pboPath, _tempDir, 5);

        result.PlayerCount.Should().Be(3);
    }

    [Fact]
    public async Task PatchMission_ShouldCatchExceptions_AndReturnFailure()
    {
        var pboPath = CreateTestPbo();

        _mockPboTools.Setup(x => x.ExtractPbo(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("Extract failed"));

        var result = await _service.PatchMission(pboPath, _tempDir, 5);

        result.Success.Should().BeFalse();
        result.Reports.Should().HaveCount(1);
        result.Reports[0].Error.Should().BeTrue();
        _mockLogger.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task PatchMission_ShouldCleanupFolder_AfterSuccess()
    {
        var pboPath = CreateTestPbo();
        var folderName = Path.GetFileNameWithoutExtension(pboPath);
        SetupExtractToCreateMissionFolder(pboPath);

        await _service.PatchMission(pboPath, _tempDir, 5);

        var folderPath = Path.Combine(_tempDir, folderName);
        Directory.Exists(folderPath).Should().BeFalse();
    }

    [Fact]
    public async Task PatchMission_ShouldCleanupFolder_AfterFailure()
    {
        var pboPath = CreateTestPbo();
        var folderName = Path.GetFileNameWithoutExtension(pboPath);
        SetupExtractToCreateMissionFolder(pboPath, includeDescriptionExt: false);

        await _service.PatchMission(pboPath, _tempDir, 5);

        var folderPath = Path.Combine(_tempDir, folderName);
        Directory.Exists(folderPath).Should().BeFalse();
    }

    private string CreateTestPbo()
    {
        var pboPath = Path.Combine(_tempDir, "testmission.Altis.pbo");
        File.WriteAllText(pboPath, "fake pbo content");
        return pboPath;
    }

    private void SetupExtractToCreateMissionFolder(
        string pboPath,
        bool includeDescriptionExt = true,
        bool useSimplePack = false,
        bool includeAllDescriptionItems = false,
        int playableCount = 0
    )
    {
        var folderName = Path.GetFileNameWithoutExtension(pboPath);
        var folderPath = Path.Combine(_tempDir, folderName);

        _mockPboTools.Setup(x => x.ExtractPbo(It.IsAny<string>(), It.IsAny<string>()))
                     .Returns(Task.CompletedTask)
                     .Callback(() =>
                         {
                             Directory.CreateDirectory(folderPath);
                             if (includeDescriptionExt)
                             {
                                 var descContent = "maxPlayers = 10;";
                                 if (useSimplePack) descContent += "\nmissionUseSimplePack = 1;";
                                 if (includeAllDescriptionItems)
                                 {
                                     descContent += "\nonLoadName = \"Custom Op\";";
                                     descContent += "\nonLoadMission = \"Custom Op\";";
                                     descContent += "\noverviewText = \"Custom Op\";";
                                 }

                                 File.WriteAllText(Path.Combine(folderPath, "description.ext"), descContent);
                             }

                             File.WriteAllText(Path.Combine(folderPath, "cba_settings.sqf"), "uksf_curator_curatorsMax = 5;");
                             CreateSqm(folderPath, playableCount);
                         }
                     );
    }

    private static void CreateSqm(string folderPath, int playableCount)
    {
        var lines = new[]
        {
            "version=54;",
            "class ItemIDProvider",
            "{",
            $"nextID={playableCount + 10};",
            "};",
            "class Mission",
            "{",
            "class Entities",
            "{",
            $"items = {playableCount};"
        };
        var items = Enumerable.Range(0, playableCount)
                              .SelectMany(i => new[]
                                  {
                                      $"class Item{i}",
                                      "{",
                                      "dataType=\"Object\";",
                                      $"id={i + 10};",
                                      "isPlayable=1;",
                                      "side=\"West\";",
                                      "type=\"UKSF_B_Rifleman\";",
                                      "};"
                                  }
                              );
        var footer = new[] { "};", "};" };
        File.WriteAllLines(Path.Combine(folderPath, "mission.sqm"), lines.Concat(items).Concat(footer));
    }

    private static IUnitsContext CreateMockUnitsContext()
    {
        var mock = new Mock<IUnitsContext>();
        var rootUnit = new DomainUnit
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Name = "Root",
            Branch = UnitBranch.Combat,
            Parent = MongoDB.Bson.ObjectId.Empty.ToString(),
            Callsign = "Root"
        };
        mock.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new[] { rootUnit });
        return mock.Object;
    }
}
