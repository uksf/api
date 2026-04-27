using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaMissions.Tests.Helpers;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class MissionPatchingIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _backupDir;
    private readonly string _modsDir;
    private readonly string _capturedDir;
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IPboTools> _mockPboTools = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<ISqmDecompiler> _mockDecompiler = new();
    private readonly MissionPatchingService _service;
    private readonly TestPatchDataBuilder _testData;

    private static readonly string TestDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");

    public MissionPatchingIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_integration_{Guid.NewGuid():N}");
        _backupDir = Path.Combine(Path.GetTempPath(), $"uksf_backup_{Guid.NewGuid():N}");
        _modsDir = Path.Combine(Path.GetTempPath(), $"uksf_mods_{Guid.NewGuid():N}");
        _capturedDir = Path.Combine(Path.GetTempPath(), $"uksf_captured_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_backupDir);
        Directory.CreateDirectory(_modsDir);
        Directory.CreateDirectory(_capturedDir);

        _mockVariablesService.Setup(x => x.GetVariable("MISSIONS_BACKUPS")).Returns(new DomainVariableItem { Key = "MISSIONS_BACKUPS", Item = _backupDir });
        _mockVariablesService.Setup(x => x.GetVariable("MISSIONS_WORKING_DIR"))
                             .Returns(new DomainVariableItem { Key = "MISSIONS_WORKING_DIR", Item = _tempDir });

        _mockDecompiler.Setup(x => x.IsBinarized(It.IsAny<string>())).ReturnsAsync(false);

        _testData = new TestPatchDataBuilder();
        var patchDataBuilder = _testData.BuildPatchDataBuilder();
        var pboHandler = new PboHandler(_mockPboTools.Object, _mockVariablesService.Object);
        var sqmReader = new SqmReader();
        var sqmWriter = new SqmWriter();
        var sqmPatcher = new SqmPatcher();
        var descReader = new DescriptionReader();
        var descWriter = new DescriptionWriter();
        var descPatcher = new DescriptionPatcher();
        var settingsReader = new SettingsReader();

        var headlessClientPatcher = new HeadlessClientPatcher(_mockVariablesService.Object);

        _service = new MissionPatchingService(
            pboHandler,
            sqmReader,
            sqmWriter,
            sqmPatcher,
            descReader,
            descWriter,
            descPatcher,
            settingsReader,
            patchDataBuilder,
            _mockDecompiler.Object,
            headlessClientPatcher,
            _mockLogger.Object
        );
    }

    public void Dispose()
    {
        SafeDeleteDirectory(_tempDir);
        SafeDeleteDirectory(_backupDir);
        SafeDeleteDirectory(_modsDir);
        SafeDeleteDirectory(_capturedDir);
    }

    // ─── Test 1: Standard Mission ─────────────────────────────────────────

    [Fact]
    public async Task StandardMission_PatchesCorrectly()
    {
        var folderPath = SetupMission("Base", preExtractAction: folder => { File.WriteAllText(Path.Combine(folder, "README.txt"), "Delete me"); });

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();
        result.Reports.Should().OnlyContain(r => !r.Error);

        var sqmLines = ReadCapturedFile("mission.sqm");

        // Non-playable Object preserved
        sqmLines.Should().Contain(l => l.Contains("B_Soldier_F"));

        // New groups added with correct callsigns (UKSF root pruned: no direct members, not permanent)
        AssertCallsignInSqm(sqmLines, "Guardian");
        AssertCallsignInSqm(sqmLines, "Kestrel");
        AssertCallsignInSqm(sqmLines, "Raider");
        AssertCallsignInSqm(sqmLines, "Claymore");
        AssertCallsignInSqm(sqmLines, "Reserves");
        AssertCallsignInSqm(sqmLines, "JSFAW");
        AssertCallsignInSqm(sqmLines, "Sniper Platoon");
        AssertCallsignInSqm(sqmLines, "3 Medical Regiment");

        // 8 curators from cba_settings
        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(8);

        // Player classes
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Pilot"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Sniper"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Medic"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_SectionLeader"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Rifleman"));

        // Medic and Engineer traits present
        sqmLines.Should().Contain(l => l.Contains("Enh_unitTraits_medic"));
        sqmLines.Should().Contain(l => l.Contains("Enh_unitTraits_engineer"));

        // isPlayable count matches PlayerCount
        var playableCount = CountPlayable(sqmLines);
        playableCount.Should().BeGreaterThan(0);
        result.PlayerCount.Should().Be(playableCount);

        // README was deleted (not in captured output)
        File.Exists(Path.Combine(_capturedDir, "README.txt")).Should().BeFalse();

        // Description patched
        var descLines = ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("maxPlayers") && l.Contains(playableCount.ToString()));

        // Item indices sequential
        AssertSequentialItemIndices(sqmLines);
    }

    // ─── Test 2: @ignore Tag Preserved ────────────────────────────────────

    [Fact]
    public async Task IgnoreTagPreserved_KeepsIgnoredGroup()
    {
        SetupMission("Base", sqmOverride: "IgnoreTag");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();

        var sqmLines = ReadCapturedFile("mission.sqm");

        sqmLines.Should().Contain(l => l.Contains("@ignore"));
        AssertCallsignInSqm(sqmLines, "Kestrel");

        var playableCount = CountPlayable(sqmLines);
        result.PlayerCount.Should().Be(playableCount);
    }

    // ─── Test 3: Patching Ignored Flag ────────────────────────────────────

    [Fact]
    public async Task PatchingIgnoredFlag_SkipsSqmPatching()
    {
        SetupMission("Base", descOverride: "PatchingIgnored");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => r.Title.Contains("Mission Patching Ignored"));

        var sqmLines = ReadCapturedFile("mission.sqm");

        // Original playable group preserved (SQM not patched)
        sqmLines.Should().Contain(l => l.Contains("Player 1@Alpha"));

        // Description still patched
        var descLines = ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("maxPlayers"));
    }

    // ─── Test 4: Missing description.ext ──────────────────────────────────

    [Fact]
    public async Task MissingDescriptionExt_ReturnsError()
    {
        SetupMission("Base", preExtractAction: folder => { File.Delete(Path.Combine(folder, "description.ext")); });

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("description.ext"));

        // Pack should not have been called
        _mockPboTools.Verify(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ─── Test 5: Missing cba_settings.sqf ─────────────────────────────────

    [Fact]
    public async Task MissingCbaSettings_ReturnsError()
    {
        SetupMission("Base", preExtractAction: folder => { File.Delete(Path.Combine(folder, "cba_settings.sqf")); });

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("cba_settings.sqf"));

        _mockPboTools.Verify(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ─── Test 6: Missing Configurable Items ───────────────────────────────

    [Fact]
    public async Task MissingConfigurableItems_ReturnsErrors()
    {
        SetupMission("Base", descOverride: "MissingConfigurable");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadName"));
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadMission"));
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("overviewText"));
    }

    // ─── Test 7: Default Configurable Items ───────────────────────────────

    [Fact]
    public async Task DefaultConfigurableItems_ReturnsWarnings()
    {
        SetupMission("Base", descOverride: "DefaultConfigurable");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("onLoadName"));
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("onLoadMission"));
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("overviewText"));
    }

    // ─── Test 8: Non-Default Required Items ───────────────────────────────

    [Fact]
    public async Task NonDefaultRequiredItems_ReturnsWarning()
    {
        SetupMission("Base", descOverride: "NonDefaultRequired");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("author"));
    }

    // ─── Test 9: Missing Required Items Auto-Appended ─────────────────────

    [Fact]
    public async Task MissingRequiredItems_AutoAppended()
    {
        SetupMission("Base", descOverride: "MissingRequired");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();
        result.Reports.Should().OnlyContain(r => !r.Error);

        var descLines = ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("allowProfileGlasses"));
        descLines.Should().Contain(l => l.Contains("cba_settings_hasSettingsFile"));
    }

    // ─── Test 10: Missing maxPlayers ──────────────────────────────────────

    [Fact]
    public async Task MissingMaxPlayers_ReturnsError()
    {
        SetupMission("Base", descOverride: "MissingMaxPlayers");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("maxPlayers"));
    }

    // ─── Test 11: Curators Fallback to Server Default ─────────────────────

    [Fact]
    public async Task CuratorsFallbackServerDefault_UsesServerValue()
    {
        SetupMission("Base", cbaOverride: "CuratorsNoSetting");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 6);

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => r.Title.Contains("uksf_curator_curatorsMax"));

        var sqmLines = ReadCapturedFile("mission.sqm");
        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(6);
    }

    // ─── Test 12: Curators Malformed Setting ──────────────────────────────

    [Fact]
    public async Task CuratorsMalformedSetting_FallsBackToHardcoded()
    {
        SetupMission("Base", cbaOverride: "CuratorsMalformed");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 6);

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => r.Title.Contains("uksf_curator_curatorsMax"));

        var sqmLines = ReadCapturedFile("mission.sqm");
        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(5);
    }

    // ─── Test 13: README Deleted ──────────────────────────────────────────

    [Fact]
    public async Task ReadmeDeleted_RemovedFromMissionFolder()
    {
        SetupMission("Base", preExtractAction: folder => { File.WriteAllText(Path.Combine(folder, "README.txt"), "Delete me"); });

        await _service.PatchMission(GetPboPath(), _modsDir, 5);

        File.Exists(Path.Combine(_capturedDir, "README.txt")).Should().BeFalse();
    }

    // ─── Test 14: __EXEC Lines Removed ────────────────────────────────────

    [Fact]
    public async Task ExecLinesRemoved_StrippedFromOutput()
    {
        SetupMission("Base", descOverride: "ExecLines");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();

        var descLines = ReadCapturedFile("description.ext");
        descLines.Should().NotContain(l => l.Contains("__EXEC"));
    }

    // ─── Test 15: Real Mission ────────────────────────────────────────────

    [Fact]
    public async Task RealMission_FullPatchingSucceeds()
    {
        SetupMission("RealMission");

        var result = await _service.PatchMission(GetPboPath(), _modsDir, 5);

        result.Success.Should().BeTrue();

        var sqmLines = ReadCapturedFile("mission.sqm");

        // Non-playable entities preserved
        sqmLines.Should().Contain(l => l.Contains("O_Soldier_F"));
        sqmLines.Should().Contain(l => l.Contains("ModuleEndMission_F"));
        sqmLines.Should().Contain(l => l.Contains("Land_CampingTable_F"));

        // UKSF groups created
        AssertCallsignInSqm(sqmLines, "Kestrel");
        AssertCallsignInSqm(sqmLines, "JSFAW");

        // 8 curators
        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(8);

        // Player count correct
        var playableCount = CountPlayable(sqmLines);
        result.PlayerCount.Should().Be(playableCount);

        // Known warnings: default configurable items
        result.Reports.Should().Contain(r => r.Title.Contains("onLoadName"));
        result.Reports.Should().Contain(r => r.Title.Contains("onLoadMission"));
        result.Reports.Should().Contain(r => r.Title.Contains("overviewText"));

        // Missing allowProfileGlasses auto-appended
        var descLines = ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("allowProfileGlasses"));

        AssertSequentialItemIndices(sqmLines);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private string GetPboPath() => Path.Combine(_tempDir, "testmission.Altis.pbo");

    private string GetMissionFolderPath() => Path.Combine(_tempDir, "testmission.Altis");

    private string SetupMission(
        string baseScenario,
        string descOverride = null,
        string sqmOverride = null,
        string cbaOverride = null,
        Action<string> preExtractAction = null
    )
    {
        var pboPath = GetPboPath();
        File.WriteAllText(pboPath, "fake pbo content");

        var folderPath = GetMissionFolderPath();

        _mockPboTools.Setup(x => x.ExtractPbo(It.IsAny<string>(), It.IsAny<string>()))
                     .Returns(Task.CompletedTask)
                     .Callback(() =>
                         {
                             Directory.CreateDirectory(folderPath);
                             CopyTestDataToFolder(baseScenario, folderPath);

                             if (sqmOverride != null) CopyFileOverride(sqmOverride, "mission.sqm", folderPath);
                             if (descOverride != null) CopyFileOverride(descOverride, "description.ext", folderPath);
                             if (cbaOverride != null) CopyFileOverride(cbaOverride, "cba_settings.sqf", folderPath);

                             preExtractAction?.Invoke(folderPath);
                         }
                     );

        // Capture output files during pack (before Cleanup deletes the folder)
        _mockPboTools.Setup(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                     .Returns(Task.CompletedTask)
                     .Callback<string, string, string>((folder, _, _) => CaptureFolder(folder));
        _mockPboTools.Setup(x => x.SimplePackPbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                     .Returns(Task.CompletedTask)
                     .Callback<string, string, string>((folder, _, _) => CaptureFolder(folder));

        return folderPath;
    }

    private void CaptureFolder(string sourceFolder)
    {
        foreach (var file in Directory.GetFiles(sourceFolder))
        {
            File.Copy(file, Path.Combine(_capturedDir, Path.GetFileName(file)), true);
        }
    }

    private List<string> ReadCapturedFile(string fileName)
    {
        var path = Path.Combine(_capturedDir, fileName);
        File.Exists(path).Should().BeTrue($"captured file '{fileName}' should exist");
        return File.ReadAllLines(path).ToList();
    }

    private static void CopyTestDataToFolder(string scenario, string targetFolder)
    {
        var sourceDir = Path.Combine(TestDataDir, scenario);
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Test data directory not found: {sourceDir}");
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetFolder, Path.GetFileName(file)), true);
        }
    }

    private static void CopyFileOverride(string scenario, string fileName, string targetFolder)
    {
        var sourceFile = Path.Combine(TestDataDir, scenario, fileName);
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException($"Override file not found: {sourceFile}");
        }

        File.Copy(sourceFile, Path.Combine(targetFolder, fileName), true);
    }

    private static void AssertCallsignInSqm(List<string> sqmLines, string callsign)
    {
        sqmLines.Should().Contain(l => l.Contains($"value=\"{callsign}\""), $"expected callsign '{callsign}' in SQM");
    }

    private static int CountPlayable(List<string> sqmLines)
    {
        return sqmLines.Count(l => l.Trim().Replace(" ", "").Contains("isPlayable=1"));
    }

    private static int CountOccurrences(List<string> lines, string text)
    {
        return lines.Count(l => l.Contains(text));
    }

    private static void AssertSequentialItemIndices(List<string> sqmLines)
    {
        var entitiesIndex = sqmLines.FindIndex(l => l.Trim() == "class Entities");
        if (entitiesIndex == -1) return;

        var itemsCountLine = sqmLines.Skip(entitiesIndex).FirstOrDefault(l => l.Trim().StartsWith("items"));
        if (itemsCountLine == null) return;

        var countStr = itemsCountLine.Split('=')[1].Replace(";", "").Trim();
        if (!int.TryParse(countStr, out var expectedCount)) return;

        var itemLines = sqmLines.Skip(entitiesIndex)
                                .Where(l => l.Trim().StartsWith("class Item") && !l.Trim().StartsWith("class ItemIDProvider"))
                                .Select(l => l.Trim())
                                .ToList();

        for (var i = 0; i < expectedCount; i++)
        {
            itemLines.Should().Contain($"class Item{i}", $"expected Item{i} in sequential indices");
        }
    }

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
