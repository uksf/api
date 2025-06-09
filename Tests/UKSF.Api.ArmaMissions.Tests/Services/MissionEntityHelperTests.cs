using System;
using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class MissionEntityHelperTests
{
    [Fact]
    public void CreateFromItems_ShouldReturnMissionEntity_WhenValidItemsProvided()
    {
        // Arrange
        var items = new List<string>
        {
            "items = 1;",
            "class Item0",
            "{",
            "    dataType = \"Object\";",
            "    class PositionInfo",
            "    {",
            "        position[] = {1000, 5, 1000};",
            "    };",
            "    id = 1;",
            "    type = \"ModuleCurator_F\";",
            "};"
        };

        // Act
        var result = MissionEntityHelper.CreateFromItems(items);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MissionEntity>();
        result.ItemsCount.Should().Be(1);
    }

    [Fact]
    public void CreateFromItems_ShouldReturnEmptyEntity_WhenItemsCountIsZero()
    {
        // Arrange
        var items = new List<string> { "items = 0;" };

        // Act
        var result = MissionEntityHelper.CreateFromItems(items);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MissionEntity>();
        result.ItemsCount.Should().Be(0);
        result.MissionEntityItems.Count.Should().Be(0);
    }

    [Fact]
    public void CreateFromItems_ShouldThrowNullReferenceException_WhenItemsIsNull()
    {
        // Act & Assert
        var act = () => MissionEntityHelper.CreateFromItems(null!);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void CreateFromItems_ShouldHandleMultipleItems_WhenComplexItemsProvided()
    {
        // Arrange
        var items = new List<string>
        {
            "items = 2;",
            "class Item0",
            "{",
            "    dataType = \"Group\";",
            "    class Entities",
            "    {",
            "        items = 1;",
            "        class Item0",
            "        {",
            "            dataType = \"Object\";",
            "            id = 1;",
            "        };",
            "    };",
            "};",
            "class Item1",
            "{",
            "    dataType = \"Object\";",
            "    id = 2;",
            "};"
        };

        // Act
        var result = MissionEntityHelper.CreateFromItems(items);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MissionEntity>();
        result.ItemsCount.Should().Be(2);
    }

    [Fact]
    public void CreateFromItems_ShouldParseItemsCount_WhenItemsCountSpecified()
    {
        // Arrange
        var items = new List<string>
        {
            "items = 3;",
            "class Item0",
            "{",
            "    dataType = \"Object\";",
            "    id = 1;",
            "};",
            "class Item1",
            "{",
            "    dataType = \"Object\";",
            "    id = 2;",
            "};",
            "class Item2",
            "{",
            "    dataType = \"Object\";",
            "    id = 3;",
            "};"
        };

        // Act
        var result = MissionEntityHelper.CreateFromItems(items);

        // Assert
        result.Should().NotBeNull();
        result.ItemsCount.Should().Be(3);
    }

    [Fact]
    public void CreateFromItems_ShouldThrowFormatException_WhenItemsCountMalformed()
    {
        // Arrange - Missing items count
        var items = new List<string>
        {
            "class Item0",
            "{",
            "    dataType = \"Object\";",
            "    id = 1;",
            "};"
        };

        // Act & Assert
        var act = () => MissionEntityHelper.CreateFromItems(items);
        act.Should().Throw<FormatException>();
    }
}
