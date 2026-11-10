using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Exceptions;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class RapDecoderTests
{
    [Fact]
    public void A_class_that_points_at_its_own_body_is_rejected()
    {
        var bytes = Rap([0, 1, 0, ..Asciiz("X"), ..Offset(16)]);

        Decode(bytes).Should().Throw<PboOperationException>().WithMessage("*nests deeper than 64");
    }

    [Fact]
    public void Two_classes_that_point_at_each_other_are_rejected()
    {
        List<byte> first = [0, 1, 0, ..Asciiz("A")];
        var secondOffset = 16 + first.Count + 4;
        byte[] second = [0, 1, 0, ..Asciiz("B"), ..Offset(16)];

        Decode(Rap([..first, ..Offset(secondOffset), ..second])).Should().Throw<PboOperationException>().WithMessage("*nests deeper than 64");
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Nested_classes_decode_up_to_the_depth_limit(int depth, bool decodes)
    {
        List<byte> body = [];
        for (var i = 0; i < depth; i++)
        {
            body.AddRange([0, 1, 0, ..Asciiz("C"), ..Offset(16 + (i + 1) * 9)]);
        }

        body.AddRange([0, 0]);

        var act = Decode(Rap(body));

        if (decodes)
        {
            act().Split('\n').Count(line => line.Trim() == "class C").Should().Be(64);
        }
        else
        {
            act.Should().Throw<PboOperationException>().WithMessage("*nests deeper than 64");
        }
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Nested_arrays_decode_up_to_the_depth_limit(int depth, bool decodes)
    {
        List<byte> body = [0, 1, 2, ..Asciiz("a"), 1];
        for (var i = 1; i < depth; i++)
        {
            body.AddRange([3, 1]);
        }

        body[^1] = 0;

        var act = Decode(Rap(body));

        if (decodes)
        {
            act().Should().Be($"a[] = {new string('{', depth)}{new string('}', depth)};\n");
        }
        else
        {
            act.Should().Throw<PboOperationException>().WithMessage("*nests deeper than 64");
        }
    }

    [Fact]
    public void Bodies_that_reference_the_next_body_twice_are_stopped_by_the_budget()
    {
        List<byte> body = [];
        const int Levels = 30;
        for (var i = 0; i < Levels; i++)
        {
            var next = Offset(16 + (i + 1) * 16);
            body.AddRange([0, 2, 0, ..Asciiz("A"), ..next, 0, ..Asciiz("B"), ..next]);
        }

        body.AddRange([0, 0]);

        Decode(Rap(body)).Should().Throw<PboOperationException>().WithMessage("*more than*");
    }

    [Fact]
    public void An_array_count_larger_than_the_file_is_rejected_before_allocation()
    {
        Decode(Rap([0, 1, 2, ..Asciiz("a"), 0xFF, 0xFF, 0xFF, 0xFF, 0x07])).Should().Throw<PboOperationException>().WithMessage("*exceeds the file");
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, "*exceeds the file")]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 }, "*overflows")]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 }, "*overflows")]
    public void An_out_of_range_root_count_is_rejected(byte[] count, string message)
    {
        Decode(Rap([0, ..count])).Should().Throw<PboOperationException>().WithMessage(message);
    }

    [Theory]
    [InlineData(0, new byte[] { 0x00 })]
    [InlineData(127, new byte[] { 0x7F })]
    [InlineData(128, new byte[] { 0x80, 0x01 })]
    public void Root_counts_decode_at_the_varint_boundaries(int count, byte[] encoded)
    {
        var entries = Enumerable.Repeat<byte[]>([3, ..Asciiz("X")], count).SelectMany(x => x);

        Decode(Rap([0, ..encoded, ..entries]))().Should().Be(string.Concat(Enumerable.Repeat("class X;\n", count)));
    }

    [Theory]
    [InlineData("hint \"a\";\nhint \"b\";", "\"hint \"\"a\"\";\" \\n \"hint \"\"b\"\";\"")]
    [InlineData("a\r\nb", "\"a\" \\n \"b\"")]
    [InlineData("a\rb", "\"a\" \\n \"b\"")]
    [InlineData("\"\n\"", "\"\"\"\" \\n \"\"\"\"")]
    [InlineData("a\n", "\"a\" \\n \"\"")]
    public void Strings_with_line_breaks_use_the_config_newline_form(string value, string expected)
    {
        Decode(Rap([0, 1, 1, 0, ..Asciiz("init"), ..Asciiz(value)]))().Should().Be($"init = {expected};\n");
    }

    [Fact]
    public void Strings_in_nested_arrays_use_the_config_newline_form()
    {
        Decode(Rap([0, 1, 2, ..Asciiz("a"), 1, 3, 1, 0, ..Asciiz("x\ny")]))().Should().Be("a[] = {{\"x\" \\n \"y\"}};\n");
    }

    private static Func<string> Decode(IEnumerable<byte> bytes)
    {
        var array = bytes.ToArray();
        return () => RapDecoder.ToText(array);
    }

    private static byte[] Rap(IEnumerable<byte> root)
    {
        return [0, (byte)'r', (byte)'a', (byte)'P', 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, ..root, 0, 0, 0, 0];
    }

    private static byte[] Offset(int value)
    {
        return BitConverter.GetBytes(value);
    }

    private static byte[] Asciiz(string value)
    {
        return [..Encoding.UTF8.GetBytes(value), 0];
    }
}
