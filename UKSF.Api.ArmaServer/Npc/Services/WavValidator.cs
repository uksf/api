using System;

namespace UKSF.Api.ArmaServer.Npc.Services;

public readonly record struct WavInfo(bool Valid, string Error, long DurationMs);

// Minimal PCM-WAV header parser: validates RIFF/WAVE, locates fmt + data chunks, computes duration.
// Tolerant of extra chunks (LIST/INFO/fact) by walking the chunk table.
public static class WavValidator
{
    public static WavInfo Parse(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 44)
        {
            return new WavInfo(false, "File too small to be a WAV", 0);
        }

        if (Ascii(bytes, 0, 4) != "RIFF" || Ascii(bytes, 8, 4) != "WAVE")
        {
            return new WavInfo(false, "Not a RIFF/WAVE file", 0);
        }

        int sampleRate = 0, byteRate = 0;
        short channels = 0, bits = 0, format = 0;
        long dataBytes = 0;
        var hasFmt = false;
        var hasData = false;

        var pos = 12;
        while (pos + 8 <= bytes.Length)
        {
            var chunkId = Ascii(bytes, pos, 4);
            var chunkSize = BitConverter.ToInt32(bytes, pos + 4);
            if (chunkSize < 0)
            {
                break;
            }

            var body = pos + 8;
            if (chunkId == "fmt " && body + 16 <= bytes.Length)
            {
                format = BitConverter.ToInt16(bytes, body);
                channels = BitConverter.ToInt16(bytes, body + 2);
                sampleRate = BitConverter.ToInt32(bytes, body + 4);
                byteRate = BitConverter.ToInt32(bytes, body + 8);
                bits = BitConverter.ToInt16(bytes, body + 14);
                hasFmt = true;
            }
            else if (chunkId == "data")
            {
                dataBytes = Math.Min(chunkSize, bytes.Length - body);
                hasData = true;
            }

            pos = body + chunkSize + (chunkSize % 2); // chunks are word-aligned
        }

        if (!hasFmt || !hasData)
        {
            return new WavInfo(false, "Missing fmt or data chunk", 0);
        }

        if (format != 1) // 1 = PCM; reject float/compressed (wrong duration math + the clone engine expects PCM)
        {
            return new WavInfo(false, "Only uncompressed PCM WAV is supported", 0);
        }

        var divisor = byteRate > 0 ? byteRate : sampleRate * channels * (bits / 8);
        if (divisor <= 0)
        {
            return new WavInfo(false, "Invalid WAV format header", 0);
        }

        var durationMs = (long)(dataBytes * 1000.0 / divisor);
        return new WavInfo(true, string.Empty, durationMs);
    }

    private static string Ascii(byte[] b, int offset, int len)
    {
        if (offset + len > b.Length)
        {
            return string.Empty;
        }

        return System.Text.Encoding.ASCII.GetString(b, offset, len);
    }
}
