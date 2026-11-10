using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Exceptions;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public sealed class SqmDecompilerTests : IDisposable
{
    private readonly SqmDecompiler _decompiler = new();
    private readonly string _sqmPath = Path.Combine(Directory.CreateTempSubdirectory("uksf_sqm_test_").FullName, "mission.sqm");

    public void Dispose()
    {
        Directory.Delete(Path.GetDirectoryName(_sqmPath)!, true);
    }

    [Fact]
    public async Task IsBinarized_is_true_for_a_rapified_file()
    {
        File.WriteAllBytes(_sqmPath, Rapify());

        (await _decompiler.IsBinarized(_sqmPath)).Should().BeTrue();
    }

    [Theory]
    [InlineData("version=54;")]
    [InlineData("")]
    public async Task IsBinarized_is_false_for_text(string text)
    {
        File.WriteAllText(_sqmPath, text);

        (await _decompiler.IsBinarized(_sqmPath)).Should().BeFalse();
    }

    [Fact]
    public async Task Decompile_writes_every_entry_type_as_config_text()
    {
        File.WriteAllBytes(_sqmPath, Rapify());

        await _decompiler.Decompile(_sqmPath);

        File.ReadAllText(_sqmPath)
            .Should()
            .Be(
                "version = 54;\n" +
                "class Mission: Base\n{\n" +
                "\tname = \"He said \"\"hi\"\"\";\n" +
                "\tgrid = 1.5707964;\n" +
                "\ttiny = -9.205038e-05;\n" +
                "\tposition[] = {1.5,{2,\"x\"}};\n" +
                "\tclass Ext;\n" +
                "\tdelete Old;\n" +
                "\tkeys[] += {3};\n" +
                "};\n"
            );
    }

    [Fact]
    public async Task Decompile_rejects_a_truncated_file()
    {
        File.WriteAllBytes(_sqmPath, Rapify()[..40]);

        var act = () => _decompiler.Decompile(_sqmPath);

        await act.Should().ThrowAsync<PboOperationException>();
    }

    private static byte[] Rapify()
    {
        List<byte> mission =
        [
            ..Asciiz("Base"), 7,
            1, 0, ..Asciiz("name"), ..Asciiz("He said \"hi\""),
            1, 1, ..Asciiz("grid"), ..BitConverter.GetBytes(1.5707964f),
            1, 1, ..Asciiz("tiny"), ..BitConverter.GetBytes(-9.205038e-05f),
            2, ..Asciiz("position"), 2, 1, ..BitConverter.GetBytes(1.5f), 3, 2, 2, ..BitConverter.GetBytes(2), 0, ..Asciiz("x"),
            3, ..Asciiz("Ext"),
            4, ..Asciiz("Old"),
            5, 1, 0, 0, 0, ..Asciiz("keys"), 1, 2, ..BitConverter.GetBytes(3)
        ];
        List<byte> root = [0, 2, 1, 2, ..Asciiz("version"), ..BitConverter.GetBytes(54), 0, ..Asciiz("Mission")];
        var missionOffset = 16 + root.Count + 4;
        root.AddRange(BitConverter.GetBytes(missionOffset));
        var enumOffset = missionOffset + mission.Count;
        return [0, (byte)'r', (byte)'a', (byte)'P', 0, 0, 0, 0, 8, 0, 0, 0, ..BitConverter.GetBytes(enumOffset), ..root, ..mission, 0, 0, 0, 0];
    }

    private static byte[] Asciiz(string value)
    {
        return [..Encoding.ASCII.GetBytes(value), 0];
    }
}
