using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class DescriptionPatcherTests
{
    private readonly DescriptionPatcher _subject = new();

    private static MissionPatchContext CreateContext(List<string> lines, int playerCount = 20)
    {
        return new MissionPatchContext { Description = new DescriptionDocument { Lines = lines }, PlayerCount = playerCount };
    }

    [Fact]
    public void Patch_UpdatesMaxPlayersLine_WithContextPlayerCount()
    {
        var context = CreateContext(["    maxPlayers = 10;"], playerCount: 35);

        _subject.Patch(context);

        context.Description.Lines.Should().Contain("    maxPlayers = 35;");
        context.Reports.Should().NotContain(r => r.Title.Contains("maxPlayers"));
    }

    [Fact]
    public void Patch_WhenMaxPlayersMissing_AddsErrorReport()
    {
        var context = CreateContext(["author = \"UKSF\";"]);

        _subject.Patch(context);

        context.Reports.Should().ContainSingle(r => r.Error && r.Title.Contains("maxPlayers"));
    }

    [Fact]
    public void Patch_RequiredItemPresentWithDefaultValue_AddsNoReport()
    {
        var context = CreateContext(["    maxPlayers = 10;", "    author = \"UKSF\";"]);

        _subject.Patch(context);

        context.Reports.Should().NotContain(r => r.Title.Contains("author"));
    }

    [Fact]
    public void Patch_RequiredItemPresentWithNonDefaultValue_AddsWarningReport()
    {
        var context = CreateContext(["    maxPlayers = 10;", "    author = \"SomeOtherAuthor\";"]);

        _subject.Patch(context);

        context.Reports.Should().ContainSingle(r => !r.Error && r.Title.Contains("author") && r.Title.Contains("Warning"));
    }

    [Fact]
    public void Patch_RequiredItemMissing_AppendsLineWithDefaultValue()
    {
        var context = CreateContext(["    maxPlayers = 10;"]);

        _subject.Patch(context);

        context.Description.Lines.Should().Contain("author = \"UKSF\";");
    }

    [Fact]
    public void Patch_ConfigurableItemPresentWithDefaultValue_AddsWarningReport()
    {
        var context = CreateContext(["    maxPlayers = 10;", "    onLoadName = \"UKSF: Operation\";"]);

        _subject.Patch(context);

        context.Reports.Should().ContainSingle(r => !r.Error && r.Title.Contains("onLoadName") && r.Title.Contains("Warning"));
    }

    [Fact]
    public void Patch_ConfigurableItemMissing_AddsErrorReport()
    {
        var context = CreateContext(["    maxPlayers = 10;"]);

        _subject.Patch(context);

        context.Reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadName"));
        context.Reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadMission"));
        context.Reports.Should().Contain(r => r.Error && r.Title.Contains("overviewText"));
    }

    [Fact]
    public void Patch_RemovesScalarEnableDebugConsoleLine()
    {
        var context = CreateContext(
            [
                "    maxPlayers = 10;",
                "    enableDebugConsole = 1;",
                "    author = \"UKSF\";"
            ]
        );

        _subject.Patch(context);

        context.Description.Lines.Should().NotContain(l => l.Contains("enableDebugConsole"));
        context.Description.Lines.Should().Contain("    author = \"UKSF\";");
    }

    [Fact]
    public void Patch_RemovesScalarEnableDebugConsoleLine_CaseInsensitive()
    {
        var context = CreateContext(
            [
                "    maxPlayers = 10;",
                "    EnableDebugConsole = 2;"
            ]
        );

        _subject.Patch(context);

        context.Description.Lines.Should().NotContain(l => l.ToLowerInvariant().Contains("debugconsole"));
    }

    [Fact]
    public void Patch_RemovesSingleLineEnableDebugConsoleArray()
    {
        var context = CreateContext(
            [
                "    maxPlayers = 10;",
                "    enableDebugConsole[] = { \"76561198000000000\", \"76561198000000001\" };",
                "    author = \"UKSF\";"
            ]
        );

        _subject.Patch(context);

        context.Description.Lines.Should().NotContain(l => l.Contains("enableDebugConsole"));
        context.Description.Lines.Should().NotContain(l => l.Contains("76561198"));
    }

    [Fact]
    public void Patch_RemovesMultiLineEnableDebugConsoleArray()
    {
        var context = CreateContext(
            [
                "    maxPlayers = 10;",
                "    enableDebugConsole[] =",
                "    {",
                "        \"76561198000000000\",",
                "        \"76561198000000001\"",
                "    };",
                "    author = \"UKSF\";"
            ]
        );

        _subject.Patch(context);

        context.Description.Lines.Should().NotContain(l => l.Contains("enableDebugConsole"));
        context.Description.Lines.Should().NotContain(l => l.Contains("76561198"));
        context.Description.Lines.Should().Contain("    author = \"UKSF\";");
    }

    [Fact]
    public void Patch_UnterminatedEnableDebugConsoleBlock_LeavesLinesIntact()
    {
        var context = CreateContext(
            [
                "    maxPlayers = 10;",
                "    enableDebugConsole[] =",
                "    {",
                "        \"76561198000000000\""
            ]
        );

        _subject.Patch(context);

        context.Description.Lines.Should().Contain(l => l.Contains("enableDebugConsole"));
        context.Description.Lines.Should().Contain(l => l.Contains("maxPlayers"));
    }

    [Fact]
    public void Patch_RemovesLinesContainingExec()
    {
        var context = CreateContext(
            [
                "    maxPlayers = 10;",
                "    someExec = __EXEC(thing);",
                "    author = \"UKSF\";",
                "    __EXEC anotherExecLine;"
            ]
        );

        _subject.Patch(context);

        context.Description.Lines.Should().NotContain(l => l.Contains("__EXEC"));
        context.Description.Lines.Should().Contain("    maxPlayers = 20;");
        context.Description.Lines.Should().Contain("    author = \"UKSF\";");
    }

    [Fact]
    public void Patch_CombinationOfIssues_ProducesCorrectReportsAndLines()
    {
        var context = CreateContext(
            [
                "    respawn = \"CUSTOM\";",
                "    onLoadName = \"UKSF: Operation\";",
                "    __EXEC someExec;"
            ],
            playerCount: 40
        );

        _subject.Patch(context);

        context.Reports.Should().Contain(r => r.Error && r.Title.Contains("maxPlayers"));
        context.Reports.Should().Contain(r => !r.Error && r.Title.Contains("respawn") && r.Title.Contains("Warning"));
        context.Reports.Should().Contain(r => !r.Error && r.Title.Contains("onLoadName") && r.Title.Contains("Warning"));
        context.Reports.Should().Contain(r => r.Error && r.Title.Contains("onLoadMission"));
        context.Reports.Should().Contain(r => r.Error && r.Title.Contains("overviewText"));

        context.Description.Lines.Should().NotContain(l => l.Contains("__EXEC"));
        context.Description.Lines.Should().Contain("author = \"UKSF\";");
        context.Description.Lines.Should().Contain("loadScreen = \"uksf.paa\";");
    }
}
