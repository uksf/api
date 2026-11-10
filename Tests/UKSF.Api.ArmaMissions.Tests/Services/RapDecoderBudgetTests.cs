using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Exceptions;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class RapDecoderBudgetTests
{
    public static TheoryData<string, byte[], string> Cases =>
        new()
        {
            { "LF string", Rap([0, 1, 1, 0, ..Asciiz("x"), ..Asciiz("a\nb")]), "x = \"a\" \\n \"b\";\n" },
            { "CRLF string", Rap([0, 1, 1, 0, ..Asciiz("x"), ..Asciiz("a\r\nb")]), "x = \"a\" \\n \"b\";\n" },
            { "CR string", Rap([0, 1, 1, 0, ..Asciiz("x"), ..Asciiz("a\rb")]), "x = \"a\" \\n \"b\";\n" },
            { "quote string", Rap([0, 1, 1, 0, ..Asciiz("x"), ..Asciiz("\"\"")]), "x = \"\"\"\"\"\";\n" },
            { "flat array", Rap([0, 1, 2, ..Asciiz("a"), 2, 2, ..BitConverter.GetBytes(1), 2, ..BitConverter.GetBytes(2)]), "a[] = {1,2};\n" },
            { "nested array", Rap([0, 1, 2, ..Asciiz("a"), 1, 3, 1, 0, ..Asciiz("x\ny")]), "a[] = {{\"x\" \\n \"y\"}};\n" },
            { "class with parent", Rap([0, 1, 0, ..Asciiz("Child"), ..BitConverter.GetBytes(29), ..Asciiz("Parent"), 0]), "class Child: Parent\n{\n};\n" },
            { "long name", Rap([0, 1, 3, ..Asciiz(new string('n', 40))]), $"class {new string('n', 40)};\n" }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Output_that_fits_the_budget_exactly_decodes(string name, byte[] bytes, string expected)
    {
        RapDecoder.ToText(bytes, expected.Length).Should().Be(expected, name);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Output_one_character_over_the_budget_is_rejected(string name, byte[] bytes, string expected)
    {
        var act = () => RapDecoder.ToText(bytes, expected.Length - 1);

        act.Should().Throw<PboOperationException>(name).WithMessage($"rapified file decodes to more than {expected.Length - 1} characters");
    }

    [Fact]
    public void A_long_string_deep_in_nested_arrays_is_rejected_at_the_budget()
    {
        List<byte> body = [0, 1, 2, ..Asciiz("a")];
        for (var i = 0; i < 63; i++)
        {
            body.AddRange([1, 3]);
        }

        body.AddRange([1, 0, ..Asciiz(new string('\n', 100_000))]);

        var act = () => RapDecoder.ToText(Rap(body), 1000);

        act.Should().Throw<PboOperationException>().WithMessage("rapified file decodes to more than 1000 characters");
    }

    private static byte[] Rap(IEnumerable<byte> root)
    {
        return [0, (byte)'r', (byte)'a', (byte)'P', 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, ..root.ToArray(), 0, 0, 0, 0];
    }

    private static byte[] Asciiz(string value)
    {
        return [..Encoding.UTF8.GetBytes(value), 0];
    }
}
