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

    public void Dispose()
    {
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Extract_counts_shared_folders_once_for_each_entry()
    {
        string pboPath = PboFixture.Write(
            Path.Combine(_tempDir, "op.pbo"),
            ("a\\x.sqf", 0u, Array.Empty<byte>(), 0u),
            ("a\\y.sqf", 0u, Array.Empty<byte>(), 0u)
        );

        Action act = () => PboReader.Extract(pboPath, OutputFolder, maxPathComponents: 3);

        act.Should().Throw<PboOperationException>().WithMessage("pbo paths have more than 3 folders and files");
    }

    [Fact]
    public void Extract_rejects_a_trailing_separator()
    {
        string pboPath = PboFixture.Write(Path.Combine(_tempDir, "op.pbo"), ("scripts\\", 0u, new byte[] { 1 }, 1u));

        Action act = () => PboReader.Extract(pboPath, OutputFolder);

        act.Should().Throw<PboOperationException>().WithMessage("scripts\\: unsafe path in pbo");
    }

    [Fact]
    public void Extract_rejects_distinct_nested_trees_past_the_path_component_limit_before_writing()
    {
        string pboPath = WriteNestedFiles(3);

        Action act = () => PboReader.Extract(pboPath, OutputFolder, maxPathComponents: 11);

        act.Should().Throw<PboOperationException>().WithMessage("pbo paths have more than 11 folders and files");
        Directory.Exists(OutputFolder).Should().BeFalse();
    }

    [Fact]
    public void Extract_rejects_more_entries_than_the_limit_before_writing()
    {
        string pboPath = WriteEmptyFiles(4);

        Action act = () => PboReader.Extract(pboPath, OutputFolder, 3);

        act.Should().Throw<PboOperationException>().WithMessage("pbo has more than 3 files");
        Directory.Exists(OutputFolder).Should().BeFalse();
    }

    [Fact]
    public void Extract_writes_distinct_nested_trees_at_the_path_component_limit()
    {
        string pboPath = WriteNestedFiles(2);

        PboReader.Extract(pboPath, OutputFolder, maxPathComponents: 8);

        PboFixture.ReadTree(OutputFolder).Keys.Should().BeEquivalentTo(Nested("0"), Nested("1"));
    }

    [Fact]
    public void Extract_writes_every_file_at_the_entry_limit()
    {
        string pboPath = WriteEmptyFiles(3);

        PboReader.Extract(pboPath, OutputFolder, 3);

        PboFixture.ReadTree(OutputFolder).Keys.Should().BeEquivalentTo("0.sqf", "1.sqf", "2.sqf");
    }

    private string OutputFolder => Path.Combine(_tempDir, "out");

    [Theory]
    [InlineData("scripts/init.sqf", "scripts//init.sqf")]
    [InlineData("scripts\\init.sqf", "scripts\\\\init.sqf")]
    [InlineData("scripts\\init.sqf", "scripts/\\init.sqf")]
    public void Extract_rejects_an_empty_path_segment_before_writing(string first, string second)
    {
        string pboPath = PboFixture.Write(Path.Combine(_tempDir, "op.pbo"), (first, 0u, new byte[] { 1 }, 1u), (second, 0u, new byte[] { 2 }, 1u));

        Action act = () => PboReader.Extract(pboPath, OutputFolder);

        act.Should().Throw<PboOperationException>().WithMessage($"{second}: unsafe path in pbo");
        Directory.Exists(OutputFolder).Should().BeFalse();
    }

    private string WriteNestedFiles(int count)
    {
        (string, uint, byte[], uint)[] entries = Enumerable.Range(0, count).Select(i => ($"{i}\\a\\a\\x.sqf", 0u, Array.Empty<byte>(), 0u)).ToArray();
        return PboFixture.Write(Path.Combine(_tempDir, "op.pbo"), entries);
    }

    private static string Nested(string root) => Path.Combine(root, "a", "a", "x.sqf");

    private string WriteEmptyFiles(int count)
    {
        (string, uint, byte[], uint)[] entries = Enumerable.Range(0, count).Select(i => ($"{i}.sqf", 0u, Array.Empty<byte>(), 0u)).ToArray();
        return PboFixture.Write(Path.Combine(_tempDir, "op.pbo"), entries);
    }
}
