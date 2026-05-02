using System;
using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Parsing;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Parsing;

public class SqfNotationParserTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsEmptyString()
    {
        SqfNotationParser.Parse("\"\"").Should().Be(string.Empty);
    }

    [Fact]
    public void Parse_SimpleString_ReturnsContent()
    {
        SqfNotationParser.Parse("\"hello\"").Should().Be("hello");
    }

    [Fact]
    public void Parse_StringWithDoubledQuote_CollapsesToSingleQuote()
    {
        // SQF: he said "yes"  → str → "he said ""yes"""
        SqfNotationParser.Parse("\"he said \"\"yes\"\"\"").Should().Be("he said \"yes\"");
    }

    [Fact]
    public void Parse_StringOnlyDoubledQuote_ReturnsSingleQuote()
    {
        // SQF: "  → str → """"   (open, doubled-quote, close)
        SqfNotationParser.Parse("\"\"\"\"").Should().Be("\"");
    }

    [Fact]
    public void Parse_StringWithLiteralBackslash_PreservesBackslash()
    {
        // SQF strings preserve backslash literally; no escape semantics.
        SqfNotationParser.Parse("\"path\\to\\file\"").Should().Be("path\\to\\file");
    }

    [Fact]
    public void Parse_StringOnlyBackslash_PreservesIt()
    {
        SqfNotationParser.Parse("\"\\\"").Should().Be("\\");
    }

    [Fact]
    public void Parse_StringWithLiteralNewline_PreservesIt()
    {
        SqfNotationParser.Parse("\"hi\nbye\"").Should().Be("hi\nbye");
    }

    [Fact]
    public void Parse_StringWithLiteralTab_PreservesIt()
    {
        SqfNotationParser.Parse("\"a\tb\"").Should().Be("a\tb");
    }

    [Fact]
    public void Parse_StringWithUnicode_PreservesIt()
    {
        SqfNotationParser.Parse("\"café\"").Should().Be("café");
    }

    [Fact]
    public void Parse_StringWithBrackets_PreservesContent()
    {
        SqfNotationParser.Parse("\"[a,b,c]\"").Should().Be("[a,b,c]");
    }

    [Fact]
    public void Parse_IntegerZero_ReturnsLong()
    {
        SqfNotationParser.Parse("0").Should().Be(0L);
    }

    [Fact]
    public void Parse_PositiveInteger_ReturnsLong()
    {
        SqfNotationParser.Parse("42").Should().Be(42L);
    }

    [Fact]
    public void Parse_NegativeInteger_ReturnsLong()
    {
        SqfNotationParser.Parse("-42").Should().Be(-42L);
    }

    [Fact]
    public void Parse_Float_ReturnsDouble()
    {
        SqfNotationParser.Parse("3.14159").Should().Be(3.14159);
    }

    [Fact]
    public void Parse_NegativeFloat_ReturnsDouble()
    {
        SqfNotationParser.Parse("-3.14159").Should().Be(-3.14159);
    }

    [Fact]
    public void Parse_TinyFloat_ReturnsDouble()
    {
        SqfNotationParser.Parse("0.0001").Should().Be(0.0001);
    }

    [Fact]
    public void Parse_ScientificNotation_ReturnsDouble()
    {
        var result = SqfNotationParser.Parse("1.23457e+09");
        result.Should().BeOfType<double>();
        ((double)result).Should().BeApproximately(1.23457e+9, 1.0);
    }

    [Fact]
    public void Parse_ScientificNoFraction_ReturnsDouble()
    {
        SqfNotationParser.Parse("1e+10").Should().Be(1e+10);
    }

    [Fact]
    public void Parse_ScientificNegative_ReturnsDouble()
    {
        SqfNotationParser.Parse("-1e+10").Should().Be(-1e+10);
    }

    [Fact]
    public void Parse_True_ReturnsBool()
    {
        SqfNotationParser.Parse("true").Should().Be(true);
    }

    [Fact]
    public void Parse_False_ReturnsBool()
    {
        SqfNotationParser.Parse("false").Should().Be(false);
    }

    [Fact]
    public void Parse_Any_ReturnsNull()
    {
        SqfNotationParser.Parse("any").Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyArray_ReturnsEmptyList()
    {
        SqfNotationParser.Parse("[]").Should().BeEquivalentTo(new List<object>());
    }

    [Fact]
    public void Parse_SingletonArray_ReturnsList()
    {
        SqfNotationParser.Parse("[1]").Should().BeEquivalentTo(new List<object> { 1L });
    }

    [Fact]
    public void Parse_MixedTypeArray_ReturnsList()
    {
        var result = SqfNotationParser.Parse("[1,\"a\",true,[2,3]]");
        result.Should()
        .BeEquivalentTo(
            new List<object>
            {
                1L,
                "a",
                true,
                new List<object> { 2L, 3L }
            }
        );
    }

    [Fact]
    public void Parse_NestedArray_ReturnsNestedList()
    {
        var result = SqfNotationParser.Parse("[[1,2],[3,4],[5,6]]");
        result.Should()
        .BeEquivalentTo(
            new List<object>
            {
                new List<object> { 1L, 2L },
                new List<object> { 3L, 4L },
                new List<object> { 5L, 6L }
            }
        );
    }

    [Fact]
    public void Parse_ArrayWithNil_ReturnsListWithNull()
    {
        SqfNotationParser.Parse("[1,any,2]")
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                1L,
                null,
                2L
            }
        );
    }

    [Fact]
    public void Parse_ArrayWithEmptyString_ReturnsListWithEmpty()
    {
        SqfNotationParser.Parse("[\"\"]").Should().BeEquivalentTo(new List<object> { string.Empty });
    }

    [Fact]
    public void Parse_PairListShape_ReturnsListOfPairs()
    {
        // HashMaps and pair-lists are syntactically identical via str(); parser returns list-of-lists.
        var result = SqfNotationParser.Parse("[[\"a\",1],[\"b\",\"two\"],[\"c\",true]]");
        result.Should()
        .BeEquivalentTo(
            new List<object>
            {
                new List<object> { "a", 1L },
                new List<object> { "b", "two" },
                new List<object> { "c", true }
            }
        );
    }

    [Fact]
    public void Parse_NestedHashmapShape_ReturnsListOfNestedPairs()
    {
        var result = SqfNotationParser.Parse("[[\"outer\",[[\"inner\",[1,2,3]]]]]");
        result.Should()
        .BeEquivalentTo(
            new List<object>
            {
                new List<object>
                {
                    "outer",
                    new List<object>
                    {
                        new List<object>
                        {
                            "inner",
                            new List<object>
                            {
                                1L,
                                2L,
                                3L
                            }
                        }
                    }
                }
            }
        );
    }

    [Fact]
    public void Parse_HashmapWithSpecialCharsInKeysAndValues_ReturnsCorrectStrings()
    {
        // Includes: doubled quote, literal backslash, empty value, space in key, doubled quote in key
        // Keys and values use SQF "" doubling for embedded quotes, backslash literal.
        var input = "[[\"hasquote\",\"a\"\"b\"],[\"hasbackslash\",\"a\\b\"],[\"empty\",\"\"],[\"key with spaces\",\"val\"],[\"key\"\"with\"\"quote\",\"val\"]]";
        var result = SqfNotationParser.Parse(input);
        result.Should()
        .BeEquivalentTo(
            new List<object>
            {
                new List<object> { "hasquote", "a\"b" },
                new List<object> { "hasbackslash", "a\\b" },
                new List<object> { "empty", "" },
                new List<object> { "key with spaces", "val" },
                new List<object> { "key\"with\"quote", "val" }
            }
        );
    }

    [Fact]
    public void Parse_WithLeadingAndTrailingWhitespace_Tolerated()
    {
        SqfNotationParser.Parse("  [1,2]  ").Should().BeEquivalentTo(new List<object> { 1L, 2L });
    }

    [Fact]
    public void Parse_WithWhitespaceAroundCommas_Tolerated()
    {
        SqfNotationParser.Parse("[1 , 2 , 3]")
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                1L,
                2L,
                3L
            }
        );
    }

    [Fact]
    public void Parse_RealObjectSample_ParsesAllFields()
    {
        // Replica of the str() output of a fake persistence object hashmap (Probe #6).
        var input = "[[\"id\",\"fake_obj_1\"],[\"type\",\"Land_PortableLight_F\"],[\"position\",[11000,3700,21]],[\"damage\",0.123456],[\"customName\",\"\"]]";
        var result = SqfNotationParser.Parse(input);
        result.Should().BeOfType<List<object>>();
        var list = (List<object>)result;
        list.Should().HaveCount(5);
        list[0].Should().BeEquivalentTo(new List<object> { "id", "fake_obj_1" });
        list[2]
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                "position",
                new List<object>
                {
                    11000L,
                    3700L,
                    21L
                }
            }
        );
        list[3].Should().BeEquivalentTo(new List<object> { "damage", 0.123456 });
        list[4].Should().BeEquivalentTo(new List<object> { "customName", "" });
    }

    [Fact]
    public void Parse_UnterminatedString_Throws()
    {
        Action action = () => SqfNotationParser.Parse("\"hello");
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_UnterminatedArray_Throws()
    {
        Action action = () => SqfNotationParser.Parse("[1,2");
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_TrailingContent_Throws()
    {
        Action action = () => SqfNotationParser.Parse("42 garbage");
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_BadKeyword_Throws()
    {
        Action action = () => SqfNotationParser.Parse("trueeee");
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_InvalidNumber_Throws()
    {
        Action action = () => SqfNotationParser.Parse("-");
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseAndNormalize_PairListWithStringKeys_ReturnsDictionary()
    {
        var result = SqfNotationParser.ParseAndNormalize("[[\"a\",1],[\"b\",2]]");
        result.Should().BeOfType<Dictionary<string, object>>();
        var dict = (Dictionary<string, object>)result;
        dict.Should().ContainKey("a").WhoseValue.Should().Be(1L);
        dict.Should().ContainKey("b").WhoseValue.Should().Be(2L);
    }

    [Fact]
    public void ParseAndNormalize_NestedHashmaps_RecursivelyConverts()
    {
        var input = "[[\"outer\",[[\"inner\",[1,2,3]]]]]";
        var result = SqfNotationParser.ParseAndNormalize(input);
        result.Should().BeOfType<Dictionary<string, object>>();
        var outerDict = (Dictionary<string, object>)result;
        outerDict["outer"].Should().BeOfType<Dictionary<string, object>>();
        var innerDict = (Dictionary<string, object>)outerDict["outer"];
        innerDict["inner"]
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                1L,
                2L,
                3L
            }
        );
    }

    [Fact]
    public void ParseAndNormalize_PositionalTwoElementArrayWithNonStringFirst_StaysAsList()
    {
        // Persistence "inventory" weapons positional pair: [<list of classNames>, <list of counts>].
        // First element is a list, NOT a string — must NOT be treated as hashmap.
        var input = "[[[\"AK\",\"M16\"],[30,30]]]";
        var result = SqfNotationParser.ParseAndNormalize(input);
        result.Should().BeOfType<List<object>>();
        var outer = (List<object>)result;
        outer.Should().HaveCount(1);
        outer[0].Should().BeOfType<List<object>>();
    }

    [Fact]
    public void ParseAndNormalize_EmptyArray_StaysAsEmptyList()
    {
        // Empty hashmap and empty array stringify identically as []. Stay as list.
        SqfNotationParser.ParseAndNormalize("[]").Should().BeEquivalentTo(new List<object>());
    }

    [Fact]
    public void ParseAndNormalize_AceFortify_StaysAsList()
    {
        // Persistence "aceFortify" is [bool, sideString] — first element is a bool, must stay a list.
        var input = "[false,\"west\"]";
        SqfNotationParser.ParseAndNormalize(input).Should().BeEquivalentTo(new List<object> { false, "west" });
    }

    [Fact]
    public void ParseAndNormalize_PlayerHashmapMap_ProducesNestedDicts()
    {
        // [["uid1",[["position",[1,2,3]],["damage",0]]],["uid2",[...]]]
        var input = "[[\"7656119800\",[[\"position\",[1,2,3]],[\"damage\",0]]]]";
        var result = SqfNotationParser.ParseAndNormalize(input);
        result.Should().BeOfType<Dictionary<string, object>>();
        var outer = (Dictionary<string, object>)result;
        outer.Should().ContainKey("7656119800");
        outer["7656119800"].Should().BeOfType<Dictionary<string, object>>();
        var inner = (Dictionary<string, object>)outer["7656119800"];
        inner["position"]
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                1L,
                2L,
                3L
            }
        );
        inner["damage"].Should().Be(0L);
    }

    [Fact]
    public void ParseAndNormalize_AceCargo_StaysAsListOfFourTuples()
    {
        // aceCargo: [[className, cargo, inventory, customName], ...] — 4-tuples, not 2-tuples.
        var input = "[[\"UK3CB_BAF_556_30Rnd\",[],[],\"\"]]";
        var result = SqfNotationParser.ParseAndNormalize(input);
        result.Should().BeOfType<List<object>>();
        var outer = (List<object>)result;
        outer.Should().HaveCount(1);
        outer[0].Should().BeOfType<List<object>>();
        ((List<object>)outer[0]).Should().HaveCount(4);
    }
}
