using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class WavLoudnessTests
{
    /// Minimal 16-bit mono WAV whose samples all sit at +/- `amplitude`.
    private static byte[] MakeWav(short amplitude, int samples = 200)
    {
        var body = new List<byte>();
        for (var i = 0; i < samples; i++)
        {
            body.AddRange(BitConverter.GetBytes(i % 2 == 0 ? amplitude : (short)-amplitude));
        }

        var wav = new List<byte>();
        wav.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        wav.AddRange(BitConverter.GetBytes(36 + body.Count));
        wav.AddRange(Encoding.ASCII.GetBytes("WAVEfmt "));
        wav.AddRange(BitConverter.GetBytes(16));
        wav.AddRange(BitConverter.GetBytes((short)1));
        wav.AddRange(BitConverter.GetBytes((short)1));
        wav.AddRange(BitConverter.GetBytes(24000));
        wav.AddRange(BitConverter.GetBytes(48000));
        wav.AddRange(BitConverter.GetBytes((short)2));
        wav.AddRange(BitConverter.GetBytes((short)16));
        wav.AddRange(Encoding.ASCII.GetBytes("data"));
        wav.AddRange(BitConverter.GetBytes(body.Count));
        wav.AddRange(body);
        return wav.ToArray();
    }

    [Fact]
    public void Rms_Reads_The_Sample_Level()
    {
        WavLoudness.Rms(MakeWav(1000)).Should().BeApproximately(1000, 1);
    }

    [Fact]
    public void MatchRms_Brings_A_Loud_Variant_Down_To_The_Base()
    {
        var quieter = WavLoudness.MatchRms(MakeWav(8000), WavLoudness.Rms(MakeWav(2000)));

        WavLoudness.Rms(quieter).Should().BeApproximately(2000, 20);
    }

    [Fact]
    public void MatchRms_Lifts_A_Quiet_Variant_Without_Clipping()
    {
        var louder = WavLoudness.MatchRms(MakeWav(500), 20000);

        WavLoudness.Rms(louder).Should().BeLessThanOrEqualTo(30000);
        WavLoudness.Rms(louder).Should().BeGreaterThan(500);
    }

    [Fact]
    public void MatchRms_Leaves_A_Near_Match_Alone()
    {
        var input = MakeWav(2000);

        WavLoudness.MatchRms(input, 2050).Should().BeSameAs(input);
    }

    [Fact]
    public void MatchRms_Passes_Through_Silence_And_Garbage()
    {
        var silent = MakeWav(0);
        WavLoudness.MatchRms(silent, 2000).Should().BeSameAs(silent);

        var garbage = Encoding.ASCII.GetBytes("not a wav at all");
        WavLoudness.MatchRms(garbage, 2000).Should().BeSameAs(garbage);
    }
}
