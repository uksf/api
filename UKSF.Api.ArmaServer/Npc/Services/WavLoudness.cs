using System;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Loudness matching for 16-bit PCM WAV references.
///
/// A cloned voice inherits the loudness of the sample it was cloned from, and the mood
/// engine renders hotter than the clone engine. Left alone, an angry line lands several
/// times louder than a neutral one from the same character. Matching the variant to its
/// base keeps one NPC at one volume.
public static class WavLoudness
{
    private const int HeaderScanLimit = 512;

    /// Root-mean-square of the samples, or 0 when the payload has none.
    public static double Rms(byte[] wav)
    {
        var start = DataOffset(wav);
        if (start < 0) return 0;
        long sum = 0;
        var count = 0;
        for (var i = start; i + 1 < wav.Length; i += 2)
        {
            var sample = BitConverter.ToInt16(wav, i);
            sum += (long)sample * sample;
            count++;
        }

        return count == 0 ? 0 : Math.Sqrt((double)sum / count);
    }

    /// Scale `wav` so its RMS matches `targetRms`, clamped so no sample clips. Returns the
    /// input unchanged when either side is silent or the scale is already within 5%.
    public static byte[] MatchRms(byte[] wav, double targetRms)
    {
        var rms = Rms(wav);
        if (rms <= 0 || targetRms <= 0) return wav;

        var factor = targetRms / rms;
        var start = DataOffset(wav);
        if (start < 0) return wav;

        short peak = 0;
        for (var i = start; i + 1 < wav.Length; i += 2)
        {
            var sample = Math.Abs(BitConverter.ToInt16(wav, i));
            if (sample > peak) peak = (short)Math.Min(sample, short.MaxValue);
        }

        if (peak > 0) factor = Math.Min(factor, 30000.0 / peak);
        if (Math.Abs(factor - 1.0) < 0.05) return wav;

        var output = (byte[])wav.Clone();
        for (var i = start; i + 1 < output.Length; i += 2)
        {
            var scaled = BitConverter.ToInt16(output, i) * factor;
            var clamped = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            BitConverter.GetBytes(clamped).CopyTo(output, i);
        }

        return output;
    }

    /// Byte offset of the `data` chunk payload, or -1 when this is not a WAV we can read.
    private static int DataOffset(byte[] wav)
    {
        if (wav.Length < 12) return -1;
        var limit = Math.Min(wav.Length - 8, HeaderScanLimit);
        for (var i = 12; i < limit; i++)
        {
            if (wav[i] == 'd' && wav[i + 1] == 'a' && wav[i + 2] == 't' && wav[i + 3] == 'a')
            {
                var offset = i + 8;
                return offset < wav.Length ? offset : -1;
            }
        }

        return -1;
    }
}
