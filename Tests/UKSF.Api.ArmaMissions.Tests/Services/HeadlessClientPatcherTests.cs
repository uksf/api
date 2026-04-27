using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class HeadlessClientPatcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly HeadlessClientPatcher _subject;

    public HeadlessClientPatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_hcpatcher_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _subject = new HeadlessClientPatcher(_variablesService.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private MissionPatchContext CreateContext() => new() { FolderPath = _tempDir };

    private void SetServerNames(params string[] names)
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_HEADLESS_NAMES"))
                         .Returns(new DomainVariableItem { Key = "SERVER_HEADLESS_NAMES", Item = string.Join(",", names) });
    }

    private void SetServerNamesEmpty()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_HEADLESS_NAMES")).Returns(new DomainVariableItem { Key = "SERVER_HEADLESS_NAMES", Item = "" });
    }

    private string WriteSqm(IEnumerable<string> lines)
    {
        var path = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllLines(path, lines);
        return path;
    }

    private string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static IEnumerable<string> BuildSqm(IEnumerable<string> hcSlotNames)
    {
        yield return "version=54;";
        yield return "class Mission";
        yield return "{";
        yield return "class Entities";
        yield return "{";

        var slotNames = hcSlotNames.ToList();
        yield return $"items={slotNames.Count};";

        for (var i = 0; i < slotNames.Count; i++)
        {
            yield return $"class Item{i}";
            yield return "{";
            yield return "dataType=\"Object\";";
            yield return "side=\"West\";";
            yield return $"id={100 + i};";
            yield return "type=\"HeadlessClient_F\";";
            yield return $"name=\"{slotNames[i]}\";";
            yield return "isPlayable=1;";
            yield return "};";
        }

        yield return "};";
        yield return "};";
    }

    // ─── No-op cases ────────────────────────────────────────────────────────

    [Fact]
    public void Patch_NoSqmFile_NoOp()
    {
        SetServerNames("Jarvis", "Ultron", "Vision");
        var context = CreateContext();

        _subject.Patch(context);

        context.Reports.Should().BeEmpty();
    }

    [Fact]
    public void Patch_NoServerNames_NoOp()
    {
        SetServerNamesEmpty();
        var sqmPath = WriteSqm(BuildSqm(new[] { "HC1" }));
        var original = File.ReadAllText(sqmPath);
        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(sqmPath).Should().Be(original);
        context.Reports.Should().BeEmpty();
    }

    [Fact]
    public void Patch_NoHeadlessClientSlots_NoOp()
    {
        SetServerNames("Jarvis");
        var sqmPath = WriteSqm(
            new[]
            {
                "class Mission",
                "{",
                "class Entities",
                "{",
                "items=1;",
                "class Item0",
                "{",
                "dataType=\"Object\";",
                "type=\"B_Soldier_F\";",
                "name=\"Player1\";",
                "};",
                "};",
                "};"
            }
        );
        var original = File.ReadAllText(sqmPath);
        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(sqmPath).Should().Be(original);
        context.Reports.Should().BeEmpty();
    }

    // ─── Renaming ──────────────────────────────────────────────────────────

    [Fact]
    public void Patch_MatchingCount_RenamesAllSlots()
    {
        SetServerNames("Jarvis", "Ultron", "Vision");
        WriteSqm(BuildSqm(new[] { "HC1", "HC2", "HC3" }));
        var context = CreateContext();

        _subject.Patch(context);

        var sqmContent = File.ReadAllText(Path.Combine(_tempDir, "mission.sqm"));
        sqmContent.Should().Contain("name=\"Jarvis\";");
        sqmContent.Should().Contain("name=\"Ultron\";");
        sqmContent.Should().Contain("name=\"Vision\";");
        sqmContent.Should().NotContain("name=\"HC1\";");
        sqmContent.Should().NotContain("name=\"HC2\";");
        sqmContent.Should().NotContain("name=\"HC3\";");

        context.Reports.Should().ContainSingle(r => r.Title.Contains("rewritten") && !r.Error);
    }

    [Fact]
    public void Patch_NameAlreadyMatchesServerConfig_StillPatchesOthers()
    {
        SetServerNames("HC1", "Ultron");
        WriteSqm(BuildSqm(new[] { "HC1", "HC2" }));
        var context = CreateContext();

        _subject.Patch(context);

        var sqmContent = File.ReadAllText(Path.Combine(_tempDir, "mission.sqm"));
        sqmContent.Should().Contain("name=\"HC1\";");
        sqmContent.Should().Contain("name=\"Ultron\";");
        sqmContent.Should().NotContain("name=\"HC2\";");
    }

    // ─── Reference rewriting ───────────────────────────────────────────────

    [Fact]
    public void Patch_RewritesReferencesAcrossFiles()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        WriteFile("scripts/server/show_fps.sqf", "if (!isNil \"HC1\") then {\n    if (!isNull HC1) then {\n        _sourcestr = \"HC1\";\n    };\n};\n");
        WriteFile("scripts/client/arsenal.sqf", "if (!(name _x in [\"HC1\", \"HC2\", \"HC3\"])) then { /* keep */ };\n");
        WriteFile("description.ext", "// HC1 is the headless client\nclass Header { gameType = COOP; };\n");
        WriteFile("config.cpp", "class CfgPatches { class HC1Things {}; };\n");

        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(Path.Combine(_tempDir, "scripts/server/show_fps.sqf"))
            .Should()
            .Contain("\"Jarvis\"")
            .And.Contain("isNull Jarvis")
            .And.NotContain("HC1");

        File.ReadAllText(Path.Combine(_tempDir, "scripts/client/arsenal.sqf")).Should().Contain("[\"Jarvis\", \"HC2\", \"HC3\"]");

        File.ReadAllText(Path.Combine(_tempDir, "description.ext")).Should().Contain("// HC1 is the headless client");

        File.ReadAllText(Path.Combine(_tempDir, "config.cpp")).Should().Contain("class HC1Things");
    }

    [Fact]
    public void Patch_WordBoundary_DoesNotTouchSubstringMatches()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        const string content = "_HC1Variable = 1;\nHC1_marker = \"x\";\nHC1Squad setVariable [\"k\", 1];\nfoo_HC1 = HC1;\n";
        WriteFile("scripts/test.sqf", content);

        var context = CreateContext();

        _subject.Patch(context);

        var rewritten = File.ReadAllText(Path.Combine(_tempDir, "scripts/test.sqf"));
        rewritten.Should().Contain("_HC1Variable = 1;");
        rewritten.Should().Contain("HC1_marker = \"x\";");
        rewritten.Should().Contain("HC1Squad setVariable");
        rewritten.Should().Contain("foo_HC1 = Jarvis;");
    }

    [Fact]
    public void Patch_SkipsBinaryAndNonAllowlistedFiles()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        var binaryPath = WriteFile("textures/picture.paa", "HC1 binary blob HC1");
        var pboPath = WriteFile("mission.pbo", "HC1");
        WriteFile("readme.txt", "HC1 should not be touched in txt");

        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(binaryPath).Should().Be("HC1 binary blob HC1");
        File.ReadAllText(pboPath).Should().Be("HC1");
        File.ReadAllText(Path.Combine(_tempDir, "readme.txt")).Should().Be("HC1 should not be touched in txt");
    }

    [Fact]
    public void Patch_QuotedStringWithExtraText_NotRewritten()
    {
        SetServerNames("Jarvis", "Ultron", "Vision");
        WriteSqm(BuildSqm(new[] { "HC1", "HC2", "HC3" }));

        const string content = "hint \"Merlin HC3 will be used for casevac\";\n_briefing = 'Merlin HC3 inbound';\n_x = \"UK3CB_BAF_Merlin_HC3_CSAR_DDPM\";\n";
        WriteFile("scripts/briefing.sqf", content);

        var context = CreateContext();

        _subject.Patch(context);

        var rewritten = File.ReadAllText(Path.Combine(_tempDir, "scripts/briefing.sqf"));
        rewritten.Should().Be(content);
    }

    [Fact]
    public void Patch_SingleQuotedExactMatch_Rewritten()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        WriteFile("scripts/test.sqf", "_x = 'HC1';\n");

        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(Path.Combine(_tempDir, "scripts/test.sqf")).Should().Be("_x = 'Jarvis';\n");
    }

    [Fact]
    public void Patch_LineComment_NotRewritten()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        WriteFile("scripts/notes.sqf", "// HC1 is the offload target\n_x = HC1;\n");

        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(Path.Combine(_tempDir, "scripts/notes.sqf")).Should().Be("// HC1 is the offload target\n_x = Jarvis;\n");
    }

    [Fact]
    public void Patch_BlockCommentSingleLine_NotRewritten()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        WriteFile("scripts/notes.sqf", "_a = 1; /* HC1 reference */ _b = HC1;\n");

        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(Path.Combine(_tempDir, "scripts/notes.sqf")).Should().Be("_a = 1; /* HC1 reference */ _b = Jarvis;\n");
    }

    [Fact]
    public void Patch_BlockCommentMultiLine_NotRewritten()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        WriteFile("scripts/notes.sqf", "/*\n  Merlin HC1 will be tasked\n  with casevac\n*/\n_x = HC1;\n");

        var context = CreateContext();

        _subject.Patch(context);

        File.ReadAllText(Path.Combine(_tempDir, "scripts/notes.sqf")).Should().Be("/*\n  Merlin HC1 will be tasked\n  with casevac\n*/\n_x = Jarvis;\n");
    }

    [Fact]
    public void Patch_TrailingLineCommentAfterCode_OnlyCodeRewritten()
    {
        SetServerNames("Jarvis", "Ultron", "Vision");
        WriteSqm(BuildSqm(new[] { "HC1", "HC2", "HC3" }));

        WriteFile("presets/blufor/3cbBAF_des.sqf", "[\"UK3CB_BAF_Merlin_HC3_32_DDPM\",300,0,175],   // Merlin HC3 32\n_x = HC1;\n");

        var context = CreateContext();

        _subject.Patch(context);

        var rewritten = File.ReadAllText(Path.Combine(_tempDir, "presets/blufor/3cbBAF_des.sqf"));
        rewritten.Should().Contain("UK3CB_BAF_Merlin_HC3_32_DDPM");
        rewritten.Should().Contain("// Merlin HC3 32");
        rewritten.Should().Contain("_x = Jarvis;");
    }

    [Fact]
    public void Patch_HelicopterClassnameInQuotedString_NotRewritten()
    {
        SetServerNames("Jarvis", "Ultron", "Vision");
        WriteSqm(BuildSqm(new[] { "HC1", "HC2", "HC3" }));

        WriteFile("presets/blufor/3cbBAF_des.sqf", "[\"UK3CB_BAF_Merlin_HC3_32_DDPM\",300,0,175],\n[\"UK3CB_BAF_Merlin_HC3_CSAR_DDPM\",300,80,175],\n");

        var context = CreateContext();

        _subject.Patch(context);

        var rewritten = File.ReadAllText(Path.Combine(_tempDir, "presets/blufor/3cbBAF_des.sqf"));
        rewritten.Should().Contain("UK3CB_BAF_Merlin_HC3_32_DDPM");
        rewritten.Should().Contain("UK3CB_BAF_Merlin_HC3_CSAR_DDPM");
    }

    // ─── Excess slot dropping ──────────────────────────────────────────────

    [Fact]
    public void Patch_ExcessHcs_DropsExtras_RenumbersItems()
    {
        SetServerNames("Jarvis", "Ultron");
        WriteSqm(BuildSqm(new[] { "HC1", "HC2", "HC3", "HC4" }));
        var context = CreateContext();

        _subject.Patch(context);

        var sqm = File.ReadAllText(Path.Combine(_tempDir, "mission.sqm"));
        sqm.Should().Contain("items=2;");
        sqm.Should().Contain("class Item0");
        sqm.Should().Contain("class Item1");
        sqm.Should().NotContain("class Item2");
        sqm.Should().NotContain("class Item3");
        sqm.Should().Contain("name=\"Jarvis\";");
        sqm.Should().Contain("name=\"Ultron\";");
        sqm.Should().NotContain("name=\"HC3\";");
        sqm.Should().NotContain("name=\"HC4\";");

        context.Reports.Should().Contain(r => r.Title.Contains("Excess headless client slots dropped") && r.Detail.Contains("HC3") && r.Detail.Contains("HC4"));
    }

    [Fact]
    public void Patch_AllSlotsDropped_WhenServerHasFewerThanOne()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1", "HC2" }));
        var context = CreateContext();

        _subject.Patch(context);

        var sqm = File.ReadAllText(Path.Combine(_tempDir, "mission.sqm"));
        sqm.Should().Contain("items=1;");
        sqm.Should().Contain("class Item0");
        sqm.Should().NotContain("class Item1");
        sqm.Should().Contain("name=\"Jarvis\";");
        sqm.Should().NotContain("name=\"HC2\";");
    }

    // ─── Report content ────────────────────────────────────────────────────

    [Fact]
    public void Patch_DedupesIdenticalLinesWithinFile()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        var line = "_x = HC1;";
        WriteFile("scripts/loop.sqf", string.Join("\n", Enumerable.Repeat(line, 5)));

        var context = CreateContext();

        _subject.Patch(context);

        var report = context.Reports.Single(r => r.Title.Contains("rewritten"));
        report.Detail.Should().Contain("(6 occurrences)");
        var loopSampleCount = report.Detail.Split('\n').Count(l => l.Trim().StartsWith("scripts/loop.sqf:"));
        loopSampleCount.Should().Be(1);
    }

    [Fact]
    public void Patch_CapsSamplesAtTwenty()
    {
        SetServerNames("Jarvis");
        WriteSqm(BuildSqm(new[] { "HC1" }));

        var lines = Enumerable.Range(1, 30).Select(i => $"_v{i} = HC1;").ToArray();
        WriteFile("scripts/many.sqf", string.Join("\n", lines));

        var context = CreateContext();

        _subject.Patch(context);

        var report = context.Reports.Single(r => r.Title.Contains("rewritten"));
        report.Detail.Should().Contain("...and 11 more");
        var totalSampleLines = report.Detail.Split('\n').Count(l => l.Trim().StartsWith("scripts/many.sqf:") || l.Trim().StartsWith("mission.sqm:"));
        totalSampleLines.Should().Be(20);
    }

    // ─── Group-nested HC slots ─────────────────────────────────────────────

    [Fact]
    public void Patch_LogicDataTypeHeadlessClient_StillDetected()
    {
        SetServerNames("Jarvis");
        var sqmPath = WriteSqm(
            new[]
            {
                "class Mission",
                "{",
                "class Entities",
                "{",
                "items=1;",
                "class Item0",
                "{",
                "dataType=\"Logic\";",
                "id=99;",
                "type=\"HeadlessClient_F\";",
                "name=\"HC1\";",
                "isPlayable=1;",
                "};",
                "};",
                "};"
            }
        );

        var context = CreateContext();

        _subject.Patch(context);

        var sqm = File.ReadAllText(sqmPath);
        sqm.Should().Contain("name=\"Jarvis\";");
        sqm.Should().NotContain("name=\"HC1\";");
    }

    [Fact]
    public void Patch_GroupNestedHeadlessClient_RenamedAndRefsRewritten()
    {
        SetServerNames("Jarvis");
        var sqmPath = WriteSqm(
            new[]
            {
                "class Mission",
                "{",
                "class Entities",
                "{",
                "items=1;",
                "class Item0",
                "{",
                "dataType=\"Group\";",
                "side=\"West\";",
                "id=10;",
                "class Entities",
                "{",
                "items=1;",
                "class Item0",
                "{",
                "dataType=\"Object\";",
                "id=11;",
                "type=\"HeadlessClient_F\";",
                "name=\"HC1\";",
                "isPlayable=1;",
                "};",
                "};",
                "};",
                "};",
                "};"
            }
        );

        WriteFile("scripts/test.sqf", "if (!isNull HC1) then { _x = HC1; };\n");

        var context = CreateContext();

        _subject.Patch(context);

        var sqm = File.ReadAllText(sqmPath);
        sqm.Should().Contain("name=\"Jarvis\";");
        sqm.Should().NotContain("name=\"HC1\";");

        File.ReadAllText(Path.Combine(_tempDir, "scripts/test.sqf")).Should().Contain("isNull Jarvis").And.Contain("_x = Jarvis;");
    }
}
