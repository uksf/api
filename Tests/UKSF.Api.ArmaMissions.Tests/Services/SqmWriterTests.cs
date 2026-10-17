using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmWriterTests : IDisposable
{
    private const string SafehouseTrigger =
        "onActivation=\"\"\"mkr_safehouse_4\"\" setMarkerAlphaLocal 1;\" \\n \"[\"\"<img image='\\idi\\acre\\addons\\sys_prc152\\data\\prc152c_ui.paa' size='10'/>\"\", 1] spawn BIS_fnc_dynamicText;\" \\n \"\";";

    private static readonly string Mission = string.Join(
        "\r\n",
        "version=54;",
        "class EditorData",
        "{",
        "\tclass ItemIDProvider",
        "\t{",
        "\t\tnextID=10;",
        "\t};",
        "};",
        "class Mission",
        "{",
        "\tclass Entities",
        "\t{",
        "\t\titems=3;",
        "\t\tclass Item0",
        "\t\t{",
        "\t\t\tdataType=\"Logic\";",
        "\t\t\tid=7;",
        "\t\t\ttype=\"ModuleCurator_F\";",
        "\t\t};",
        "\t\tclass Item1",
        "\t\t{",
        "\t\t\tdataType=\"Trigger\";",
        "\t\t\tclass Attributes",
        "\t\t\t{",
        $"\t\t\t\t{SafehouseTrigger}",
        "\t\t\t};",
        "\t\t\tid=8;",
        "\t\t};",
        "\t\tclass Item2",
        "\t\t{",
        "\t\t\tdataType=\"Object\";",
        "\t\t\tid=9;",
        "\t\t\ttype=\"B_Soldier_F\";",
        "\t\t};",
        "\t};",
        "};",
        ""
    );

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_sqmwriter_{Guid.NewGuid():N}");

    public SqmWriterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Write_WhenNothingChanged_WritesTheFileByteForByte()
    {
        var output = Patch(Mission, _ => { });

        output.Should().Be(Mission);
    }

    [Fact]
    public void Write_KeepsMultilineCodeWithEscapedQuotesUntouched()
    {
        var output = Patch(Mission, context => context.Sqm.Entities.RemoveAt(0));

        output.Should().Contain($"\r\n\t\t\t\t{SafehouseTrigger}\r\n");
    }

    [Fact]
    public void Write_WhenAnEntityIsRemoved_DeletesItsLinesAndRenumbersTheRest()
    {
        var output = Patch(Mission, context => context.Sqm.Entities.RemoveAt(0));

        output.Should().NotContain("ModuleCurator_F");
        output.Should().Contain("\t\titems=2;\r\n\t\tclass Item0\r\n\t\t{\r\n\t\t\tdataType=\"Trigger\";");
        output.Should().Contain("\t\tclass Item1\r\n\t\t{\r\n\t\t\tdataType=\"Object\";");
        output.Should().NotContain("class Item2");
    }

    [Fact]
    public void Write_WhenAnEntityIsAdded_AppendsItAtItemIndentationAndRaisesNextId()
    {
        var output = Patch(
            Mission,
            context => { context.Sqm.Entities.Add(new SqmLogic { Lines = ["class Item", "{", "dataType=\"Logic\";", $"id={context.NextEntityId++};", "};"] }); }
        );

        output.Should().Contain("\t\titems=4;");
        output.Should().Contain("\t\t};\r\n\t\tclass Item3\r\n\t\t{\r\n\t\t\tdataType=\"Logic\";\r\n\t\t\tid=10;\r\n\t\t};\r\n\t};\r\n};");
        output.Should().Contain("\t\tnextID=11;");
    }

    [Fact]
    public void Write_WhenUnbinHeaderPresent_StripsItsSevenLines()
    {
        var header = string.Concat(new[] { new string('/', 68) }.Concat(Enumerable.Range(2, 6).Select(i => $"// Line {i}")).Select(line => line + "\r\n"));

        var output = Patch(header + Mission, _ => { });

        output.Should().Be(Mission);
    }

    private string Patch(string sqm, Action<MissionPatchContext> change)
    {
        var context = new MissionPatchContext { FolderPath = _tempDir };
        SqmTextEditor.WriteFile(context.SqmPath, sqm);
        new SqmReader().Read(context);

        change(context);
        new SqmWriter().Write(context);

        return SqmTextEditor.ReadFile(context.SqmPath);
    }
}
