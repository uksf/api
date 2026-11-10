using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Exceptions;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public sealed class PboReaderLimitsTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("uksf_pbo_limits_").FullName;

    private string OutputFolder => Path.Combine(_tempDir, "out");

    public void Dispose()
    {
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Extract_writes_every_file_at_the_entry_limit()
    {
        var pboPath = WriteEmptyFiles(3);

        PboReader.Extract(pboPath, OutputFolder, 3);

        PboFixture.ReadTree(OutputFolder).Keys.Should().BeEquivalentTo("0.sqf", "1.sqf", "2.sqf");
    }

    [Fact]
    public void Extract_rejects_more_entries_than_the_limit_before_writing()
    {
        var pboPath = WriteEmptyFiles(4);

        var act = () => PboReader.Extract(pboPath, OutputFolder, 3);

        act.Should().Throw<PboOperationException>().WithMessage("pbo has more than 3 files");
        Directory.Exists(OutputFolder).Should().BeFalse();
    }

    [Theory]
    [InlineData("scripts/init.sqf", "scripts//init.sqf")]
    [InlineData("scripts\\init.sqf", "scripts\\\\init.sqf")]
    [InlineData("scripts\\init.sqf", "scripts/\\init.sqf")]
    public void Extract_rejects_an_empty_path_segment_before_writing(string first, string second)
    {
        var pboPath = PboFixture.Write(Path.Combine(_tempDir, "op.pbo"), (first, 0u, new byte[] { 1 }, 1u), (second, 0u, new byte[] { 2 }, 1u));

        var act = () => PboReader.Extract(pboPath, OutputFolder);

        act.Should().Throw<PboOperationException>().WithMessage($"{second}: unsafe path in pbo");
        Directory.Exists(OutputFolder).Should().BeFalse();
    }

    [Fact]
    public void Extract_rejects_a_trailing_separator()
    {
        var pboPath = PboFixture.Write(Path.Combine(_tempDir, "op.pbo"), ("scripts\\", 0u, new byte[] { 1 }, 1u));

        var act = () => PboReader.Extract(pboPath, OutputFolder);

        act.Should().Throw<PboOperationException>().WithMessage("scripts\\: unsafe path in pbo");
    }

    private string WriteEmptyFiles(int count)
    {
        var entries = Enumerable.Range(0, count).Select(i => ($"{i}.sqf", 0u, Array.Empty<byte>(), 0u)).ToArray();
        return PboFixture.Write(Path.Combine(_tempDir, "op.pbo"), entries);
    }
}
