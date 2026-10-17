using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmEdgeCaseTests : IDisposable
{
    private const string Bom = "\u00EF\u00BB\u00BF";

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_sqmedge_{Guid.NewGuid():N}");

    public SqmEdgeCaseTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Write_WhenValuesHoldComments_ParsesThemAndKeepsGeneratedIdsUnique()
    {
        var sqm = Lines(
            "class EditorData { class ItemIDProvider { nextID=0; }; };",
            "class Mission",
            "{",
            "class Entities",
            "{",
            "items=1;",
            "class Item0",
            "{",
            "dataType=\"Logic\" /* kept */;",
            "position[]={1,/* }; */2,3};",
            "id=0 /* retained entity */;",
            "};",
            "};",
            "};"
        );

        var (context, output) = Patch(sqm, AddLogic);

        context.Sqm.Entities[0].Should().BeOfType<SqmLogic>();
        Ids(output).Should().BeEquivalentTo([0, 1]);
        output.Should().Contain("nextID=2;");
    }

    [Fact]
    public void Write_WhenEntitiesIsForwardDeclaredFirst_AppendsIntoTheDefinition()
    {
        var sqm = Lines("class Mission", "{", "class Entities;", "class Entities", "{", "items=0;", "};", "};");

        var (_, output) = Patch(sqm, AddLogic);

        output.Should().Contain("class Entities;\nclass Entities\n{\nitems=1;\n\tclass Item0\n\t{\n\t\tdataType=\"Logic\";\n\t\tid=0;\n\t};\n};\n};");
    }

    [Fact]
    public void Write_WhenFileStartsWithUtf8Bom_KeepsTheBomAndAppendsEntities()
    {
        var sqm = Bom + Lines("version=54;", "class Mission", "{", "class Entities", "{", "items=0;", "};", "};");

        var (_, unchanged) = Patch(sqm, _ => { });
        var (_, added) = Patch(sqm, AddLogic);

        unchanged.Should().Be(sqm);
        added.Should().StartWith(Bom + "version=54;").And.Contain("class Item0");
    }

    [Fact]
    public void Write_WhenFirstLineIsALongerSlashSeparator_KeepsEveryLine()
    {
        var sqm = Lines(new string('/', 69), "version=54;", "class Mission", "{", "class Entities", "{", "items=0;", "};", "};");

        var (_, output) = Patch(sqm, _ => { });

        output.Should().Be(sqm);
    }

    [Fact]
    public void Write_WhenUnbinHeaderIsFollowedBySyntax_StripsOnlyTheHeaderComments()
    {
        var mission = Lines("version=54;", "class Mission", "{", "class Entities", "{", "items=0;", "};", "};");
        var sqm = Lines(new string('/', 68), "// derap header", "// generated") + mission;

        var (_, output) = Patch(sqm, _ => { });

        output.Should().Be(mission);
    }

    [Fact]
    public void Strip_WhenRemovedAttributeHoldsAnotherDebugConsoleEntry_RemovesTheAttributeWithoutOverlap()
    {
        var sqm = Lines(
            "class CustomAttributes",
            "{",
            "class Category0",
            "{",
            "class Attribute0",
            "{",
            "property=\"EnableDebugConsole\";",
            "EnableDebugConsole=1;",
            "class Value { class data { value=1; }; };",
            "};",
            "nAttributes=1;",
            "};",
            "};"
        );
        var context = new MissionPatchContext { FolderPath = _tempDir };
        SqmTextEditor.WriteFile(context.SqmPath, sqm);

        new DebugConsoleStripper().Patch(context);

        SqmTextEditor.ReadFile(context.SqmPath).Should().Be(Lines("class CustomAttributes", "{", "class Category0", "{", "nAttributes=0;", "};", "};"));
    }

    private static void AddLogic(MissionPatchContext context) =>
        context.Sqm.Entities.Add(new SqmLogic { Lines = ["class Item", "{", "dataType=\"Logic\";", $"id={context.NextEntityId++};", "};"] });

    private (MissionPatchContext Context, string Output) Patch(string sqm, Action<MissionPatchContext> change)
    {
        var context = new MissionPatchContext { FolderPath = _tempDir };
        SqmTextEditor.WriteFile(context.SqmPath, sqm);
        new SqmReader().Read(context);

        change(context);
        new SqmWriter().Write(context);

        return (context, SqmTextEditor.ReadFile(context.SqmPath));
    }

    private static int[] Ids(string output) => Regex.Matches(output, @"\bid=(\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToArray();

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + "\n"));
}
