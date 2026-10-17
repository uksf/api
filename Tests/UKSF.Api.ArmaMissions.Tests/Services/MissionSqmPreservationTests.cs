using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Tests.Helpers;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class MissionSqmPreservationTests : IDisposable
{
    private const string Crlf = "\r\n";
    private const int HighestExistingId = 43;

    private static readonly string EditorData = Block(
        "class EditorData",
        "{",
        "\tclass ItemIDProvider",
        "\t{",
        "\t\tnextID=5;",
        "\t};",
        "\tclass Camera",
        "\t{",
        "\t\tpos[]={1.5,2,3};",
        "\t};",
        "};"
    );

    private static readonly string ScenarioData = Block(
        "binarizationWanted=0;",
        "addons[]=",
        "{",
        "\t\"A3_Characters_F\"",
        "};",
        "class ScenarioData",
        "{",
        "\tauthor=\"Zo\u00e9\";",
        "\toverviewText=\"Clear the class Entities compound };\";",
        "};"
    );

    private static readonly string KeptAttribute = Block(
        "\t\tclass Attribute0",
        "\t\t{",
        "\t\t\tproperty=\"RespawnDelay\";",
        "\t\t\texpression=\"true\";",
        "\t\t};"
    );

    private static readonly string DebugConsoleAttribute = Block(
        "\t\tclass Attribute1",
        "\t\t{",
        "\t\t\tproperty=\"EnableDebugConsole\";",
        "\t\t\texpression=\"true\";",
        "\t\t\tclass Value",
        "\t\t\t{",
        "\t\t\t\tclass data",
        "\t\t\t\t{",
        "\t\t\t\t\tvalue=1;",
        "\t\t\t\t};",
        "\t\t\t};",
        "\t\t};"
    );

    private static readonly string SecondKeptAttributeBody = Block("\t\t{", "\t\t\tproperty=\"RespawnTemplates\";", "\t\t\texpression=\"false\";", "\t\t};");

    private static readonly string SoldierBody = Block(
        "\t\t{",
        "\t\t\tdataType=\"Object\";",
        "\t\t\tclass PositionInfo",
        "\t\t\t{",
        "\t\t\t\tposition[]={500,0,500};",
        "\t\t\t};",
        "\t\t\tside=\"West\";",
        "\t\t\tclass Attributes",
        "\t\t\t{",
        "\t\t\t\tinit=\"\"\"hello\"\" hint 'x';\" \\n \"if (true) then {\" \\n \"    hint \"\"a\"\";\" \\n \"};\";",
        "\t\t\t};",
        "\t\t\tid=40;",
        "\t\t\ttype=\"B_Soldier_F\";",
        "\t\t};"
    );

    private static readonly string TriggerBody = Block(
        "\t\t{",
        "\t\t\tdataType=\"Trigger\";",
        "\t\t\tclass Attributes",
        "\t\t\t{",
        "\t\t\t\tonActivation=\"\"\"mkr_safehouse_4\"\" setMarkerAlphaLocal 1; hint '<t size=''10''>x</t>';\";",
        "\t\t\t};",
        "\t\t\tid=43;",
        "\t\t};"
    );

    private readonly MissionPatchingFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Patch_KeepsEveryByteBeforeTheMissionExceptOwnedEdits()
    {
        var output = await PatchMission(Mission(EditorData));

        var expectedPrefix = MissionPrefix(EditorData)
                             .Replace(DebugConsoleAttribute, "")
                             .Replace("class Attribute2", "class Attribute1")
                             .Replace("nAttributes=3;", "nAttributes=2;")
                             .Replace("nextID=5;", $"nextID={HighestId(output) + 1};");
        output.Should().StartWith(expectedPrefix);
    }

    [Fact]
    public async Task Patch_KeepsEveryKeptEntityByteForByte()
    {
        var output = await PatchMission(Mission(EditorData));

        output.Should().Contain(SoldierBody);
        output.Should().Contain(TriggerBody);
        output.Should().EndWith(Block("\t};", "};"));
    }

    [Fact]
    public async Task Patch_WritesOnlyCrlfLineEndings()
    {
        var output = await PatchMission(Mission(EditorData));

        Regex.IsMatch(output, "(?<!\r)\n").Should().BeFalse();
    }

    [Fact]
    public async Task Patch_NewEntityIdsAreUniqueAndAboveExistingIds_WhenNextIdIsStale()
    {
        var output = await PatchMission(Mission(EditorData));

        AssertIdsUniqueAndNewIdsAbove(output);
    }

    [Fact]
    public async Task Patch_NewEntityIdsAreUniqueAndAboveExistingIds_WhenIdProviderIsMissing()
    {
        var output = await PatchMission(Mission(Block("class EditorData", "{", "};")));

        AssertIdsUniqueAndNewIdsAbove(output);
    }

    private async Task<string> PatchMission(string sqm)
    {
        _fixture.SetupMission("Base", preExtractAction: folder => File.WriteAllBytes(Path.Combine(folder, "mission.sqm"), Encoding.Latin1.GetBytes(sqm)));

        var result = await _fixture.Patch();

        result.Success.Should().BeTrue();
        return Encoding.Latin1.GetString(_fixture.ReadCapturedBytes("mission.sqm"));
    }

    private static void AssertIdsUniqueAndNewIdsAbove(string output)
    {
        var ids = Regex.Matches(output, @"\bid=(\d+);").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Except([40, 42, HighestExistingId]).Should().NotBeEmpty().And.OnlyContain(id => id > HighestExistingId);
    }

    private static int HighestId(string output) => Regex.Matches(output, @"\bid=(\d+);").Max(m => int.Parse(m.Groups[1].Value));

    private static string Mission(string editorData) =>
        MissionPrefix(editorData) +
        Block(
            "class Mission",
            "{",
            "\tclass Intel",
            "\t{",
            "\t\ttimeOfChanges=1800.0002;",
            "\t};",
            "\tclass Entities",
            "\t{",
            "\t\titems=5;",
            "\t\tclass Item0",
            "\t\t{",
            "\t\t\tdataType=\"Group\";",
            "\t\t\tside=\"West\";",
            "\t\t\tclass Entities",
            "\t\t\t{",
            "\t\t\t\titems=1;",
            "\t\t\t\tclass Item0",
            "\t\t\t\t{",
            "\t\t\t\t\tdataType=\"Object\";",
            "\t\t\t\t\tclass Attributes",
            "\t\t\t\t\t{",
            "\t\t\t\t\t\tisPlayable=1;",
            "\t\t\t\t\t\tdescription=\"Player 1@Alpha\";",
            "\t\t\t\t\t};",
            "\t\t\t\t\tid=11;",
            "\t\t\t\t\ttype=\"UKSF_B_Rifleman\";",
            "\t\t\t\t};",
            "\t\t\t};",
            "\t\t\tid=10;",
            "\t\t};",
            "\t\tclass Item1"
        ) +
        SoldierBody +
        Block(
            "\t\tclass Item2",
            "\t\t{",
            "\t\t\tdataType=\"Logic\";",
            "\t\t\tid=41;",
            "\t\t\ttype=\"ModuleCurator_F\";",
            "\t\t};",
            "\t\tclass Item3",
            "\t\t{",
            "\t\t\tdataType=\"Logic\";",
            "\t\t\tid=42;",
            "\t\t\ttype=\"Logic\";",
            "\t\t};",
            "\t\tclass Item4"
        ) +
        TriggerBody +
        Block("\t};", "};");

    private static string MissionPrefix(string editorData) =>
        Block("version=54;") +
        editorData +
        ScenarioData +
        Block("class CustomAttributes", "{", "\tclass Category0", "\t{", "\t\tname=\"Multiplayer\";") +
        KeptAttribute +
        DebugConsoleAttribute +
        Block("\t\tclass Attribute2") +
        SecondKeptAttributeBody +
        Block("\t\tnAttributes=3;", "\t};", "};");

    private static string Block(params string[] lines) => string.Concat(lines.Select(line => line + Crlf));
}
