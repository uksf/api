using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class DebugConsoleStripperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DebugConsoleStripper _subject = new();

    public DebugConsoleStripperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_debugconsole_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private MissionPatchContext CreateContext() => new() { FolderPath = _tempDir };

    private string WriteSqm(IEnumerable<string> lines)
    {
        var path = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Patch_NoSqmFile_NoOp()
    {
        var context = CreateContext();

        _subject.Patch(context);

        context.Reports.Should().BeEmpty();
    }

    [Fact]
    public void Patch_NoDebugConsoleReferences_FileUnchanged()
    {
        var sqmPath = WriteSqm(new[] { "version=54;", "class Mission", "{", "class ScenarioData", "{", "author=\"someone\";", "};", "};" });
        var original = File.ReadAllText(sqmPath);

        _subject.Patch(CreateContext());

        File.ReadAllText(sqmPath).Should().Be(original);
    }

    [Fact]
    public void Patch_ScalarEnableDebugConsoleInScenarioData_LineRemoved()
    {
        var sqmPath = WriteSqm(new[] { "class ScenarioData", "{", "author=\"someone\";", "enableDebugConsole=1;", "};" });

        _subject.Patch(CreateContext());

        var content = File.ReadAllText(sqmPath);
        content.Should().NotContain("enableDebugConsole");
        content.Should().Contain("author=\"someone\";");
    }

    [Fact]
    public void Patch_ScalarEnableDebugConsole_CaseInsensitive_LineRemoved()
    {
        var sqmPath = WriteSqm(new[] { "class ScenarioData", "{", "EnableDebugConsole = 2;", "};" });

        _subject.Patch(CreateContext());

        File.ReadAllText(sqmPath).ToLowerInvariant().Should().NotContain("debugconsole");
    }

    [Fact]
    public void Patch_CustomAttributesEnableDebugConsoleBlock_BlockRemoved()
    {
        var sqmPath = WriteSqm(
            new[]
            {
                "class CustomAttributes",
                "{",
                "class Category0",
                "{",
                "name=\"Scenario\";",
                "class Attribute0",
                "{",
                "property=\"EnableDebugConsole\";",
                "expression=\"true\";",
                "class Value",
                "{",
                "class data",
                "{",
                "class type",
                "{",
                "type[]=",
                "{",
                "\"SCALAR\"",
                "};",
                "};",
                "value=1;",
                "};",
                "};",
                "};",
                "nAttributes=1;",
                "};",
                "};"
            }
        );

        _subject.Patch(CreateContext());

        var content = File.ReadAllText(sqmPath);
        content.Should().NotContain("EnableDebugConsole");
        content.Should().NotContain("class Attribute0");
        content.Should().Contain("class Category0");
        content.Should().Contain("name=\"Scenario\";");
    }

    [Fact]
    public void Patch_CustomAttributesEnableDebugConsoleAmongOthers_OnlyMatchingBlockRemoved()
    {
        var sqmPath = WriteSqm(
            new[]
            {
                "class CustomAttributes",
                "{",
                "class Category0",
                "{",
                "class Attribute0",
                "{",
                "property=\"EnableDebugConsole\";",
                "class Value { class data { value=1; }; };",
                "};",
                "class Attribute1",
                "{",
                "property=\"SomethingElse\";",
                "class Value { class data { value=0; }; };",
                "};",
                "nAttributes=2;",
                "};",
                "};"
            }
        );

        _subject.Patch(CreateContext());

        var content = File.ReadAllText(sqmPath);
        content.Should().NotContain("EnableDebugConsole");
        content.Should().Contain("SomethingElse");
        content.Should().Contain("class Attribute1");
    }

    [Fact]
    public void Patch_BothScalarAndCustomAttributesForms_BothRemoved()
    {
        var sqmPath = WriteSqm(
            new[]
            {
                "class ScenarioData",
                "{",
                "enableDebugConsole=1;",
                "};",
                "class CustomAttributes",
                "{",
                "class Category0",
                "{",
                "class Attribute0",
                "{",
                "property=\"EnableDebugConsole\";",
                "class Value { class data { value=1; }; };",
                "};",
                "nAttributes=1;",
                "};",
                "};"
            }
        );

        _subject.Patch(CreateContext());

        var content = File.ReadAllText(sqmPath);
        content.Should().NotContain("DebugConsole");
    }

    [Fact]
    public void Patch_FileWithoutAnyMatch_NotRewritten()
    {
        var sqmPath = WriteSqm(new[] { "version=54;", "// nothing here" });
        var stampBefore = File.GetLastWriteTimeUtc(sqmPath);

        System.Threading.Thread.Sleep(50);
        _subject.Patch(CreateContext());

        File.GetLastWriteTimeUtc(sqmPath).Should().Be(stampBefore);
    }
}
