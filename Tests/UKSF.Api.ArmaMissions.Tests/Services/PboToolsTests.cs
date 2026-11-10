using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Exceptions;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public sealed class PboToolsTests : IDisposable
{
    private readonly PboTools _pboTools = new();
    private readonly string _tempDir = Directory.CreateTempSubdirectory("uksf_pbo_test_").FullName;

    public void Dispose()
    {
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task MakePbo_then_ExtractPbo_returns_the_kept_files_unchanged()
    {
        var source = CreateMission(
            new Dictionary<string, string>
            {
                ["mission.sqm"] = "version = 54;\n",
                ["description.ext"] = "onLoadName = \"Op\";\n",
                ["scripts\\init.sqf"] = "hint \"x\";\n",
                ["README.txt"] = "readme",
                ["defines.h"] = "#define X 1",
                ["scripts\\data.cpp"] = "x",
                ["Thumbs.db"] = "x",
                ["loading.PNG"] = "x"
            }
        );
        var pboPath = Path.Combine(_tempDir, "op.VR.pbo");

        await _pboTools.MakePbo(source, pboPath, _tempDir);
        await _pboTools.ExtractPbo(pboPath, Path.Combine(_tempDir, "out"));

        ReadTree(Path.Combine(_tempDir, "out", "op.VR"))
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["mission.sqm"] = "version = 54;\n",
                    ["description.ext"] = "onLoadName = \"Op\";\n",
                    [Path.Combine("scripts", "init.sqf")] = "hint \"x\";\n"
                }
            );
    }

    [Fact]
    public async Task SimplePackPbo_keeps_every_file()
    {
        var source = CreateMission(new Dictionary<string, string> { ["mission.sqm"] = "a", ["README.txt"] = "b" });
        var pboPath = Path.Combine(_tempDir, "op.VR.pbo");

        await _pboTools.SimplePackPbo(source, pboPath, _tempDir);
        await _pboTools.ExtractPbo(pboPath, Path.Combine(_tempDir, "out"));

        ReadTree(Path.Combine(_tempDir, "out", "op.VR")).Keys.Should().BeEquivalentTo("mission.sqm", "README.txt");
    }

    [Fact]
    public async Task MakePbo_writes_a_sha1_trailer_over_the_header_and_data()
    {
        var source = CreateMission(new Dictionary<string, string> { ["mission.sqm"] = "version = 54;\n" });
        var pboPath = Path.Combine(_tempDir, "op.VR.pbo");

        await _pboTools.MakePbo(source, pboPath, _tempDir);

        var bytes = File.ReadAllBytes(pboPath);
        bytes[^21].Should().Be(0);
        bytes[^20..].Should().Equal(SHA1.HashData(bytes.AsSpan(0, bytes.Length - 21)));
    }

    [Fact]
    public async Task ExtractPbo_replaces_an_existing_folder()
    {
        var pboPath = WritePbo(("mission.sqm", 0u, Encoding.ASCII.GetBytes("new"), 3u));
        var stale = Path.Combine(_tempDir, "op.VR", "stale.sqf");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "old");

        await _pboTools.ExtractPbo(pboPath, _tempDir);

        ReadTree(Path.Combine(_tempDir, "op.VR")).Should().BeEquivalentTo(new Dictionary<string, string> { ["mission.sqm"] = "new" });
    }

    [Fact]
    public async Task ExtractPbo_decompresses_lzss_entries()
    {
        byte[] block = [0x07, (byte)'a', (byte)'b', (byte)'c', 0x03, 0x03];
        var pboPath = WritePbo(("init.sqf", 0x43707273u, WithChecksum(block, "abcabcabc"), 9u));

        await _pboTools.ExtractPbo(pboPath, _tempDir);

        File.ReadAllText(Path.Combine(_tempDir, "op.VR", "init.sqf")).Should().Be("abcabcabc");
    }

    [Fact]
    public async Task ExtractPbo_rejects_a_compressed_entry_with_a_bad_checksum()
    {
        byte[] block = [0x07, (byte)'a', (byte)'b', (byte)'c', 0x03, 0x03, 1, 0, 0, 0];
        var pboPath = WritePbo(("init.sqf", 0x43707273u, block, 9u));

        var act = () => _pboTools.ExtractPbo(pboPath, _tempDir);

        await act.Should().ThrowAsync<PboOperationException>().WithMessage("init.sqf: checksum mismatch");
    }

    [Theory]
    [InlineData("..\\evil.sqf")]
    [InlineData("\\evil.sqf")]
    [InlineData("C:\\evil.sqf")]
    public async Task ExtractPbo_rejects_entries_outside_the_folder(string entryName)
    {
        var pboPath = WritePbo((entryName, 0u, Encoding.ASCII.GetBytes("x"), 1u));

        var act = () => _pboTools.ExtractPbo(pboPath, _tempDir);

        await act.Should().ThrowAsync<PboOperationException>().WithMessage($"{entryName}: unsafe path in pbo");
        File.Exists(Path.Combine(_tempDir, "evil.sqf")).Should().BeFalse();
    }

    [Fact]
    public async Task ExtractPbo_rejects_a_truncated_header()
    {
        var pboPath = Path.Combine(_tempDir, "op.VR.pbo");
        File.WriteAllBytes(pboPath, Encoding.ASCII.GetBytes("mission.sqm\0\0\0"));

        var act = () => _pboTools.ExtractPbo(pboPath, _tempDir);

        await act.Should().ThrowAsync<PboOperationException>().WithMessage("pbo header is truncated");
    }

    private static byte[] WithChecksum(byte[] block, string decoded)
    {
        var sum = (uint)Encoding.ASCII.GetBytes(decoded).Sum(x => x);
        return [..block, ..BitConverter.GetBytes(sum)];
    }

    private string WritePbo(params (string Name, uint Method, byte[] Data, uint OriginalSize)[] entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            writer.Write(new byte[1]);
            writer.Write(0x56657273u);
            writer.Write(new byte[16]);
            writer.Write(Encoding.ASCII.GetBytes("prefix\0op\0\0"));
            foreach (var entry in entries)
            {
                writer.Write(Encoding.ASCII.GetBytes(entry.Name + "\0"));
                writer.Write(entry.Method);
                writer.Write(entry.OriginalSize);
                writer.Write(0L);
                writer.Write((uint)entry.Data.Length);
            }

            writer.Write(new byte[21]);
            foreach (var entry in entries)
            {
                writer.Write(entry.Data);
            }
        }

        var pboPath = Path.Combine(_tempDir, "op.VR.pbo");
        File.WriteAllBytes(pboPath, stream.ToArray());
        return pboPath;
    }

    private string CreateMission(Dictionary<string, string> files)
    {
        var folder = Path.Combine(_tempDir, "op.VR");
        foreach (var (name, content) in files)
        {
            var path = Path.Combine(folder, name.Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        return folder;
    }

    private static Dictionary<string, string> ReadTree(string folder)
    {
        return Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(x => Path.GetRelativePath(folder, x), File.ReadAllText);
    }
}
