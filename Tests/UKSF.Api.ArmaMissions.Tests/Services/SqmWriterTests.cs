using System;
using System.IO;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqmWriter _subject = new();

    public SqmWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_sqmwriter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string[] WriteHeaderLines(params string[] headerLines)
    {
        var context = new MissionPatchContext { FolderPath = _tempDir, Sqm = new SqmDocument { HeaderLines = [..headerLines] } };

        _subject.Write(context);

        return File.ReadAllLines(context.SqmPath);
    }

    [Fact]
    public void Write_WhenQuotedPathContainsBackslashN_KeepsPathIntact()
    {
        var lines = WriteHeaderLines(@"value=""a3\air_f_jets\plane_Fighter_04\data\numbers\fighter_04_number_04_ca.paa"";");

        lines.Should().Contain(@"value=""a3\air_f_jets\plane_Fighter_04\data\numbers\fighter_04_number_04_ca.paa"";");
    }

    [Fact]
    public void Write_WhenQuotedCodeContainsSingleQuotedPath_KeepsPathIntact()
    {
        var lines = WriteHeaderLines(
            @"expression=""_this setObjectTextureGlobal [3,_value]; _this setObjectMaterialGlobal [3,'a3\air_f_jets\plane_Fighter_04\data\numbers\Fighter_04_numbers.rvmat']"";"
        );

        lines.Should()
             .Contain(
                 @"expression=""_this setObjectTextureGlobal [3,_value]; _this setObjectMaterialGlobal [3,'a3\air_f_jets\plane_Fighter_04\data\numbers\Fighter_04_numbers.rvmat']"";"
             );
    }

    [Fact]
    public void Write_WhenMultiLineCodeIsJoinedWithBackslashN_JoinsIntoSingleString()
    {
        var lines = WriteHeaderLines(@"onActivation=""[dz_2, 15] call uksf_operation_fnc_aiParadrop;"" \n ""[dz_3, 15] call uksf_operation_fnc_aiParadrop;"";");

        lines.Should().Contain(@"onActivation=""[dz_2, 15] call uksf_operation_fnc_aiParadrop;[dz_3, 15] call uksf_operation_fnc_aiParadrop;"";");
    }

    [Fact]
    public void Write_WhenJoinedCodeContainsQuotedPath_JoinsCodeAndKeepsPath()
    {
        var lines = WriteHeaderLines(@"init=""this setVariable [""""tex"""", 'a3\data\numbers\x.paa'];"" \n ""hint 'done';"";");

        lines.Should().Contain(@"init=""this setVariable ['tex', 'a3\data\numbers\x.paa'];hint 'done';"";");
    }
}
