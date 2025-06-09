using System;
using System.IO;
using FluentAssertions;
using System.Collections.Generic;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class MissionUtilitiesTests
{
    [Fact]
    public void ReadDataFromIndex_ShouldReturnEmptyList_WhenSourceIsEmpty()
    {
        // Arrange
        var source = new List<string>();
        var index = 0;

        // Act & Assert
        var act = () => MissionUtilities.ReadDataFromIndex(source, ref index);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReadDataFromIndex_ShouldReturnEmptyList_WhenIndexOutOfBounds()
    {
        // Arrange
        var source = new List<string> { "item1", "item2" };
        var index = 5;

        // Act & Assert
        var act = () => MissionUtilities.ReadDataFromIndex(source, ref index);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReadDataFromIndex_ShouldReadSingleBlock_WhenValidIndex()
    {
        // Arrange
        var source = new List<string>
        {
            "class Mission",
            "{",
            "    property = value;",
            "};"
        };
        var index = 0;

        // Act
        var result = MissionUtilities.ReadDataFromIndex(source, ref index);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("class Mission");
        result.Should().Contain("{");
        result.Should().Contain("    property = value;");
        result.Should().Contain("};");
    }

    [Fact]
    public void ReadDataFromIndex_ShouldHandleNestedBraces()
    {
        // Arrange
        var source = new List<string>
        {
            "class Outer",
            "{",
            "    class Inner",
            "    {",
            "        value = 1;",
            "    };",
            "};"
        };
        var index = 0;

        // Act
        var result = MissionUtilities.ReadDataFromIndex(source, ref index);

        // Assert
        result.Should().Contain("class Outer");
        result.Should().Contain("    class Inner");
        result.Should().Contain("        value = 1;");
        result.Count.Should().Be(7);
        result[2].Should().Be("    class Inner");
    }

    [Fact]
    public void GetIndexByKey_ShouldReturnCorrectIndex_WhenKeyExists()
    {
        // Arrange
        var source = new List<string>
        {
            "first line",
            "second line with KEY",
            "third line"
        };
        const string key = "KEY";

        // Act
        var result = MissionUtilities.GetIndexByKey(source, key);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void GetIndexByKey_ShouldReturnMinusOne_WhenKeyNotFound()
    {
        // Arrange
        var source = new List<string>
        {
            "first line",
            "second line",
            "third line"
        };
        const string key = "NOTFOUND";

        // Act
        var result = MissionUtilities.GetIndexByKey(source, key);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void GetIndexByKey_ShouldReturnFirstMatch_WhenMultipleMatches()
    {
        // Arrange
        var source = new List<string>
        {
            "first line",
            "second line with KEY",
            "third line with KEY too"
        };
        const string key = "KEY";

        // Act
        var result = MissionUtilities.GetIndexByKey(source, key);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void ReadDataByKey_ShouldReturnData_WhenKeyExists()
    {
        // Arrange
        var source = new List<string>
        {
            "other content",
            "class Mission",
            "{",
            "    property = value;",
            "};"
        };
        const string key = "class Mission";

        // Act
        var result = MissionUtilities.ReadDataByKey(source, key);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("class Mission");
    }

    [Fact]
    public void ReadDataByKey_ShouldReturnEmptyList_WhenKeyNotFound()
    {
        // Arrange
        var source = new List<string>
        {
            "first line",
            "second line",
            "third line"
        };
        const string key = "NOTFOUND";

        // Act
        var result = MissionUtilities.ReadDataByKey(source, key);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadSingleDataByKey_ShouldReturnValue_WhenKeyExistsWithEquals()
    {
        // Arrange
        var source = new List<string>
        {
            "first = value1;",
            "target = targetValue;",
            "third = value3;"
        };
        const string key = "target";

        // Act
        var result = MissionUtilities.ReadSingleDataByKey(source, key);

        // Assert
        result.Should().Be("targetValue");
    }

    [Fact]
    public void ReadSingleDataByKey_ShouldReturnValue_WhenKeyExistsWithQuotes()
    {
        // Arrange
        var source = new List<string>
        {
            "first = value1;",
            "target = \"targetValue\";",
            "third = value3;"
        };
        const string key = "target";

        // Act
        var result = MissionUtilities.ReadSingleDataByKey(source, key);

        // Assert
        result.Should().Be("targetValue");
    }

    [Fact]
    public void ReadSingleDataByKey_ShouldReturnEmptyString_WhenKeyNotFound()
    {
        // Arrange
        var source = new List<string>
        {
            "first = value1;",
            "second = value2;",
            "third = value3;"
        };
        const string key = "NOTFOUND";

        // Act
        var result = MissionUtilities.ReadSingleDataByKey(source, key);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void CheckFlag_ShouldReturnFalse_WhenMissionIsNull()
    {
        // Arrange
        Mission mission = null;
        const string key = "testFlag";

        // Act & Assert
        var act = () => MissionUtilities.CheckFlag(mission, key);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void CheckFlag_ShouldReturnFalse_WhenDescriptionPathDoesNotExist()
    {
        // Arrange
        var mission = new Mission("test/path") { DescriptionPath = "nonexistent/path/description.ext" };
        const string key = "testFlag";

        // Act & Assert
        var act = () => MissionUtilities.CheckFlag(mission, key);
        act.Should().Throw<DirectoryNotFoundException>();
    }

    // Note: Testing CheckFlag with actual file operations would require creating temp files
    // For full coverage, these tests would need to be integration tests or use a file system abstraction
}
