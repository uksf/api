using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmParsingUtilitiesTests
{
    // ─── GetIndexByKey ────────────────────────────────────────────────────

    [Fact]
    public void GetIndexByKey_ReturnsIndexOfMatchingLine()
    {
        List<string> source = ["alpha", "class Entities", "beta"];

        var result = SqmParsingUtilities.GetIndexByKey(source, "Entities");

        result.Should().Be(1);
    }

    [Fact]
    public void GetIndexByKey_IsCaseInsensitive()
    {
        List<string> source = ["first", "class MISSION", "last"];

        var result = SqmParsingUtilities.GetIndexByKey(source, "mission");

        result.Should().Be(1);
    }

    [Fact]
    public void GetIndexByKey_ReturnsFirstMatchWhenMultipleLinesMatch()
    {
        List<string> source = ["class Items", "class ItemData", "other"];

        var result = SqmParsingUtilities.GetIndexByKey(source, "item");

        result.Should().Be(0);
    }

    [Fact]
    public void GetIndexByKey_ReturnsNegativeOneWhenKeyNotFound()
    {
        List<string> source = ["alpha", "beta", "gamma"];

        var result = SqmParsingUtilities.GetIndexByKey(source, "missing");

        result.Should().Be(-1);
    }

    [Fact]
    public void GetIndexByKey_ReturnsNegativeOneForEmptySource()
    {
        List<string> source = [];

        var result = SqmParsingUtilities.GetIndexByKey(source, "anything");

        result.Should().Be(-1);
    }

    // ─── ReadBlock (ref overload) ─────────────────────────────────────────

    [Fact]
    public void ReadBlock_Ref_ReturnsCompleteBlock()
    {
        List<string> source =
        [
            "class Entities",
            "{",
            "items = 1;",
            "};"
        ];
        var index = 0;

        var result = SqmParsingUtilities.ReadBlock(source, ref index);

        result.Should().Equal("class Entities", "{", "items = 1;", "};");
    }

    [Fact]
    public void ReadBlock_Ref_AdvancesIndexPastBlock()
    {
        List<string> source =
        [
            "class Entities",
            "{",
            "items = 1;",
            "};",
            "trailing line"
        ];
        var index = 0;

        SqmParsingUtilities.ReadBlock(source, ref index);

        index.Should().Be(4);
    }

    [Fact]
    public void ReadBlock_Ref_HandlesNestedBraces()
    {
        List<string> source =
        [
            "class Outer",
            "{",
            "class Inner",
            "{",
            "value = 1;",
            "};",
            "};"
        ];
        var index = 0;

        var result = SqmParsingUtilities.ReadBlock(source, ref index);

        result.Should().Equal("class Outer", "{", "class Inner", "{", "value = 1;", "};", "};");
        index.Should().Be(7);
    }

    [Fact]
    public void ReadBlock_Ref_HandlesMultipleNestedLevels()
    {
        List<string> source =
        [
            "class Root",
            "{",
            "class A",
            "{",
            "class B",
            "{",
            "x = 0;",
            "};",
            "};",
            "};"
        ];
        var index = 0;

        var result = SqmParsingUtilities.ReadBlock(source, ref index);

        result.Should().HaveCount(10);
        result[0].Should().Be("class Root");
        result[^1].Should().Be("};");
        index.Should().Be(10);
    }

    [Fact]
    public void ReadBlock_Ref_ReturnsOnlyKeyLineWhenSourceEndsAfterKeyLine()
    {
        List<string> source = ["class Entities"];
        var index = 0;

        var result = SqmParsingUtilities.ReadBlock(source, ref index);

        result.Should().Equal("class Entities");
        index.Should().Be(1);
    }

    [Fact]
    public void ReadBlock_Ref_ReturnsEmptyWhenSourceTruncatedMidBlock()
    {
        List<string> source =
        [
            "class Entities",
            "{",
            "items = 1;"
            // missing closing };
        ];
        var index = 0;

        var result = SqmParsingUtilities.ReadBlock(source, ref index);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadBlock_Ref_ReadsFromSpecifiedIndex()
    {
        List<string> source =
        [
            "preamble",
            "class Target",
            "{",
            "data = 1;",
            "};"
        ];
        var index = 1;

        var result = SqmParsingUtilities.ReadBlock(source, ref index);

        result.Should().Equal("class Target", "{", "data = 1;", "};");
        index.Should().Be(5);
    }

    // ─── ReadBlock (non-ref overload) ─────────────────────────────────────

    [Fact]
    public void ReadBlock_NonRef_ReturnsCompleteBlock()
    {
        List<string> source =
        [
            "class Entities",
            "{",
            "items = 1;",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlock(source, 0);

        result.Should().Equal("class Entities", "{", "items = 1;", "};");
    }

    [Fact]
    public void ReadBlock_NonRef_DoesNotRequireRefArgument()
    {
        List<string> source =
        [
            "class Mission",
            "{",
            "name = \"test\";",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlock(source, 0);

        result.Should().HaveCount(4);
    }

    [Fact]
    public void ReadBlock_NonRef_HandlesNestedBraces()
    {
        List<string> source =
        [
            "class Outer",
            "{",
            "class Inner",
            "{",
            "x = 1;",
            "};",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlock(source, 0);

        result.Should().Equal("class Outer", "{", "class Inner", "{", "x = 1;", "};", "};");
    }

    [Fact]
    public void ReadBlock_NonRef_ReadsFromSpecifiedStartIndex()
    {
        List<string> source =
        [
            "ignored",
            "class Second",
            "{",
            "y = 2;",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlock(source, 1);

        result.Should().Equal("class Second", "{", "y = 2;", "};");
    }

    [Fact]
    public void ReadBlock_NonRef_ReturnsEmptyWhenTruncatedMidBlock()
    {
        List<string> source =
        [
            "class Entities",
            "{",
            "orphaned line"
            // no closing };
        ];

        var result = SqmParsingUtilities.ReadBlock(source, 0);

        result.Should().BeEmpty();
    }

    // ─── ReadBlockByKey ───────────────────────────────────────────────────

    [Fact]
    public void ReadBlockByKey_FindsAndReturnsBlock()
    {
        List<string> source =
        [
            "other = 1;",
            "class Entities",
            "{",
            "items = 3;",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlockByKey(source, "Entities");

        result.Should().Equal("class Entities", "{", "items = 3;", "};");
    }

    [Fact]
    public void ReadBlockByKey_IsCaseInsensitiveForKey()
    {
        List<string> source =
        [
            "class MISSION",
            "{",
            "name = \"test\";",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlockByKey(source, "mission");

        result.Should().HaveCount(4);
        result[0].Should().Be("class MISSION");
    }

    [Fact]
    public void ReadBlockByKey_ReturnsEmptyWhenKeyNotFound()
    {
        List<string> source =
        [
            "class Entities",
            "{",
            "items = 1;",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlockByKey(source, "nonexistent");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadBlockByKey_ReturnsEmptyForEmptySource()
    {
        List<string> source = [];

        var result = SqmParsingUtilities.ReadBlockByKey(source, "anything");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadBlockByKey_ReturnsBlockWithNestedContent()
    {
        List<string> source =
        [
            "class Groups",
            "{",
            "class Item0",
            "{",
            "side = 1;",
            "};",
            "};"
        ];

        var result = SqmParsingUtilities.ReadBlockByKey(source, "Groups");

        result.Should().Equal("class Groups", "{", "class Item0", "{", "side = 1;", "};", "};");
    }
}
