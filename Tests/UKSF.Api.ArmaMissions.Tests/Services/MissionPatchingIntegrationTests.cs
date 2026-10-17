using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Tests.Helpers;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class MissionPatchingIntegrationTests : IDisposable
{
    private readonly MissionPatchingFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task StandardMission_PatchesCorrectly()
    {
        var folderPath = _fixture.SetupMission("Base", preExtractAction: folder => { File.WriteAllText(Path.Combine(folder, "README.txt"), "Delete me"); });

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();
        result.Reports.Should().OnlyContain(r => !r.Error);

        var sqmLines = _fixture.ReadCapturedFile("mission.sqm");

        sqmLines.Should().Contain(l => l.Contains("B_Soldier_F"));

        AssertCallsignInSqm(sqmLines, "Guardian");
        AssertCallsignInSqm(sqmLines, "Kestrel");
        AssertCallsignInSqm(sqmLines, "Raider");
        AssertCallsignInSqm(sqmLines, "Claymore");
        AssertCallsignInSqm(sqmLines, "Reserves");
        AssertCallsignInSqm(sqmLines, "JSFAW");
        AssertCallsignInSqm(sqmLines, "Sniper Platoon");
        AssertCallsignInSqm(sqmLines, "3 Medical Regiment");

        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(8);

        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Pilot"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Sniper"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Medic"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_SectionLeader"));
        sqmLines.Should().Contain(l => l.Contains("UKSF_B_Rifleman"));

        sqmLines.Should().Contain(l => l.Contains("Enh_unitTraits_medic"));
        sqmLines.Should().Contain(l => l.Contains("Enh_unitTraits_engineer"));

        var playableCount = CountPlayable(sqmLines);
        playableCount.Should().BeGreaterThan(0);
        result.PlayerCount.Should().Be(playableCount);

        _fixture.CapturedFileExists("README.txt").Should().BeFalse();

        var descLines = _fixture.ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("maxPlayers") && l.Contains(playableCount.ToString()));

        AssertSequentialItemIndices(sqmLines);
    }

    [Fact]
    public async Task IgnoreTagPreserved_KeepsIgnoredGroup()
    {
        _fixture.SetupMission("Base", sqmOverride: "IgnoreTag");

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();

        var sqmLines = _fixture.ReadCapturedFile("mission.sqm");

        sqmLines.Should().Contain(l => l.Contains("@ignore"));
        AssertCallsignInSqm(sqmLines, "Kestrel");

        var playableCount = CountPlayable(sqmLines);
        result.PlayerCount.Should().Be(playableCount);
    }

    [Fact]
    public async Task PatchingIgnoredFlag_SkipsSqmPatching()
    {
        _fixture.SetupMission("Base", descOverride: "PatchingIgnored");

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => r.Title.Contains("Mission Patching Ignored"));

        var sqmLines = _fixture.ReadCapturedFile("mission.sqm");

        sqmLines.Should().Contain(l => l.Contains("Player 1@Alpha"));

        var descLines = _fixture.ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("maxPlayers"));
    }

    [Fact]
    public async Task MissingDescriptionExt_ReturnsError()
    {
        _fixture.SetupMission("Base", preExtractAction: folder => { File.Delete(Path.Combine(folder, "description.ext")); });

        var result = await _fixture.Patch();

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("description.ext"));

        _fixture.PboTools.Verify(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MissingCbaSettings_ReturnsError()
    {
        _fixture.SetupMission("Base", preExtractAction: folder => { File.Delete(Path.Combine(folder, "cba_settings.sqf")); });

        var result = await _fixture.Patch();

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("cba_settings.sqf"));

        _fixture.PboTools.Verify(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MissingConfigurableItems_ReturnsErrors()
    {
        _fixture.SetupMission("Base", descOverride: "MissingConfigurable");

        var result = await _fixture.Patch();

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadName"));
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadMission"));
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("overviewText"));
    }

    [Fact]
    public async Task DefaultConfigurableItems_ReturnsWarnings()
    {
        _fixture.SetupMission("Base", descOverride: "DefaultConfigurable");

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("onLoadName"));
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("onLoadMission"));
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("overviewText"));
    }

    [Fact]
    public async Task NonDefaultRequiredItems_ReturnsWarning()
    {
        _fixture.SetupMission("Base", descOverride: "NonDefaultRequired");

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => !r.Error && r.Title.Contains("author"));
    }

    [Fact]
    public async Task MissingRequiredItems_AutoAppended()
    {
        _fixture.SetupMission("Base", descOverride: "MissingRequired");

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();
        result.Reports.Should().OnlyContain(r => !r.Error);

        var descLines = _fixture.ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("allowProfileGlasses"));
        descLines.Should().Contain(l => l.Contains("cba_settings_hasSettingsFile"));
    }

    [Fact]
    public async Task MissingMaxPlayers_ReturnsError()
    {
        _fixture.SetupMission("Base", descOverride: "MissingMaxPlayers");

        var result = await _fixture.Patch();

        result.Success.Should().BeFalse();
        result.Reports.Should().Contain(r => r.Error && r.Title.Contains("maxPlayers"));
    }

    [Fact]
    public async Task CuratorsFallbackServerDefault_UsesServerValue()
    {
        _fixture.SetupMission("Base", cbaOverride: "CuratorsNoSetting");

        var result = await _fixture.Patch(6);

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => r.Title.Contains("uksf_curator_curatorsMax"));

        var sqmLines = _fixture.ReadCapturedFile("mission.sqm");
        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(6);
    }

    [Fact]
    public async Task CuratorsMalformedSetting_FallsBackToHardcoded()
    {
        _fixture.SetupMission("Base", cbaOverride: "CuratorsMalformed");

        var result = await _fixture.Patch(6);

        result.Success.Should().BeTrue();
        result.Reports.Should().Contain(r => r.Title.Contains("uksf_curator_curatorsMax"));

        var sqmLines = _fixture.ReadCapturedFile("mission.sqm");
        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(5);
    }

    [Fact]
    public async Task ReadmeDeleted_RemovedFromMissionFolder()
    {
        _fixture.SetupMission("Base", preExtractAction: folder => { File.WriteAllText(Path.Combine(folder, "README.txt"), "Delete me"); });

        await _fixture.Patch();

        _fixture.CapturedFileExists("README.txt").Should().BeFalse();
    }

    [Fact]
    public async Task ExecLinesRemoved_StrippedFromOutput()
    {
        _fixture.SetupMission("Base", descOverride: "ExecLines");

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();

        var descLines = _fixture.ReadCapturedFile("description.ext");
        descLines.Should().NotContain(l => l.Contains("__EXEC"));
    }

    [Fact]
    public async Task RealMission_FullPatchingSucceeds()
    {
        _fixture.SetupMission("RealMission");

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();

        var sqmLines = _fixture.ReadCapturedFile("mission.sqm");

        sqmLines.Should().Contain(l => l.Contains("O_Soldier_F"));
        sqmLines.Should().Contain(l => l.Contains("ModuleEndMission_F"));
        sqmLines.Should().Contain(l => l.Contains("Land_CampingTable_F"));

        AssertCallsignInSqm(sqmLines, "Kestrel");
        AssertCallsignInSqm(sqmLines, "JSFAW");

        CountOccurrences(sqmLines, "ModuleCurator_F").Should().Be(8);

        var playableCount = CountPlayable(sqmLines);
        result.PlayerCount.Should().Be(playableCount);

        result.Reports.Should().Contain(r => r.Title.Contains("onLoadName"));
        result.Reports.Should().Contain(r => r.Title.Contains("onLoadMission"));
        result.Reports.Should().Contain(r => r.Title.Contains("overviewText"));

        var descLines = _fixture.ReadCapturedFile("description.ext");
        descLines.Should().Contain(l => l.Contains("allowProfileGlasses"));

        AssertSequentialItemIndices(sqmLines);
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
}
