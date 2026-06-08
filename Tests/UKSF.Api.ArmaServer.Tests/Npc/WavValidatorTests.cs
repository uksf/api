using System.IO;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class WavValidatorTests
{
    // Build a minimal 16-bit PCM WAV header + `dataBytes` of silence.
    private static byte[] Wav(int sampleRate, short channels, short bits, int dataBytes)
    {
        var blockAlign = (short)(channels * bits / 8);
        var byteRate = sampleRate * blockAlign;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16); // fmt chunk size
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(bits);
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        w.Write(new byte[dataBytes]);
        w.Flush();
        return ms.ToArray();
    }

    [Fact]
    public void Parses_duration_of_a_one_second_mono_clip()
    {
        // 24000 Hz, mono, 16-bit, 1s => 24000 * 2 = 48000 data bytes
        var result = WavValidator.Parse(Wav(24000, 1, 16, 48000));
        result.Valid.Should().BeTrue();
        result.DurationMs.Should().BeInRange(990, 1010);
    }

    [Fact]
    public void Rejects_non_riff_bytes()
    {
        var result = WavValidator.Parse(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        result.Valid.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Rejects_truncated_header()
    {
        var result = WavValidator.Parse("RIFF"u8.ToArray());
        result.Valid.Should().BeFalse();
    }
}
