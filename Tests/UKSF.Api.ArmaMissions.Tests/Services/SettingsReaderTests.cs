using System;
using System.IO;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SettingsReaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsReader _subject = new();

    public SettingsReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Read_WhenFileIsMissing_AbortsWithErrorReport()
    {
        var context = new MissionPatchContext { FolderPath = _tempDir, DefaultMaxCurators = 3 };

        _subject.Read(context);

        context.Aborted.Should().BeTrue();
        context.Reports.Should().ContainSingle(r => r.Error && r.Title.Contains("cba_settings.sqf"));
    }

    [Fact]
    public void Read_WhenCuratorsMaxLineIsMissing_UsesDefaultMaxCuratorsWithWarning()
    {
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "force cba_some_other_setting = 1;");
        var context = new MissionPatchContext { FolderPath = _tempDir, DefaultMaxCurators = 3 };

        _subject.Read(context);

        context.Aborted.Should().BeFalse();
        context.MaxCurators.Should().Be(3);
        context.Reports.Should().ContainSingle(r => !r.Error && r.Title.Contains("uksf_curator_curatorsMax"));
    }

    [Fact]
    public void Read_WhenCuratorsMaxIsValid_SetsMaxCuratorsFromFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "force uksf_curator_curatorsMax = 8;");
        var context = new MissionPatchContext { FolderPath = _tempDir, DefaultMaxCurators = 3 };

        _subject.Read(context);

        context.Aborted.Should().BeFalse();
        context.MaxCurators.Should().Be(8);
        context.Reports.Should().BeEmpty();
    }

    [Fact]
    public void Read_WhenCuratorsMaxIsMalformed_FallsBackToFiveWithWarning()
    {
        File.WriteAllText(Path.Combine(_tempDir, "cba_settings.sqf"), "force uksf_curator_curatorsMax = notanumber;");
        var context = new MissionPatchContext { FolderPath = _tempDir, DefaultMaxCurators = 3 };

        _subject.Read(context);

        context.Aborted.Should().BeFalse();
        context.MaxCurators.Should().Be(5);
        context.Reports.Should().ContainSingle(r => !r.Error && r.Title.Contains("uksf_curator_curatorsMax"));
    }
}
