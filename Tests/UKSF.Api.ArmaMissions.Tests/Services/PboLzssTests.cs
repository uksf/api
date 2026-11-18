using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class PboLzssTests
{
    [Fact]
    public void Encode_shrinks_repetitive_text()
    {
        var text = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("class Item { side = \"WEST\"; };\n", 200)));

        PboLzss.Encode(text).Length.Should().BeLessThan(text.Length / 5);
    }

    public static TheoryData<string, byte[]> Inputs()
    {
        var random = new Random(7);
        var noise = new byte[20000];
        random.NextBytes(noise);
        var farRepeat = noise[..3000].Concat(new byte[5000]).Concat(noise[..3000]).ToArray();
        return new TheoryData<string, byte[]>
        {
            { "single byte", [0xFF] },
            { "run", Enumerable.Repeat((byte)' ', 10000).ToArray() },
            { "high bytes", Enumerable.Range(0, 5000).Select(i => (byte)(0x80 | (i % 7))).ToArray() },
            { "noise", noise },
            { "repeat beyond the window", farRepeat },
            { "text", Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 300).Select(i => $"_unit{i} setPos [{i}, {i * 2}, 0];\n"))) }
        };
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Encode_round_trips_through_the_reader_with_an_unsigned_checksum(string name, byte[] input)
    {
        var encoded = PboLzss.Encode(input);

        var decoded = PboReader.LzssDecode(encoded, input.Length, out var consumed);

        decoded.Should().Equal(input, name);
        consumed.Should().Be(encoded.Length - 4);
        BitConverter.ToUInt32(encoded, consumed).Should().Be((uint)input.Sum(x => (long)x));
    }
}
