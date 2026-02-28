using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

[Collection("MissionPatchData")]
public class MissionEntityHelperTests : IDisposable
{
    public void Dispose()
    {
        MissionPatchData.Instance = null;
    }

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

    [Fact]
    public void Patch_ShouldRemovePlayableGroups()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;
        SetupEmptyPatchData();

        var entity = new MissionEntity { ItemsCount = 2 };
        entity.MissionEntityItems.Add(CreatePlayableGroup());
        entity.MissionEntityItems.Add(CreateNonPlayableItem());

        entity.Patch(0);

        entity.MissionEntityItems.Should()
              .NotContain(x => x.DataType == "Group" && x.MissionEntity != null && x.MissionEntity.MissionEntityItems.All(y => y.IsPlayable));
    }

    [Fact]
    public void Patch_ShouldPreserveIgnoredPlayableGroups()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;
        SetupEmptyPatchData();

        var entity = new MissionEntity { ItemsCount = 1 };
        entity.MissionEntityItems.Add(CreateIgnoredPlayableGroup());

        entity.Patch(0);

        entity.MissionEntityItems.Should().Contain(x => x.DataType == "Group");
    }

    [Fact]
    public void Patch_ShouldRemoveExistingCuratorEntities()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;
        SetupEmptyPatchData();

        var entity = new MissionEntity { ItemsCount = 2 };
        entity.MissionEntityItems.Add(
            new MissionEntityItem
            {
                DataType = "Logic",
                Type = "ModuleCurator_F",
                RawMissionEntityItem = ["class Item0", "{", "};"]
            }
        );
        entity.MissionEntityItems.Add(CreateNonPlayableItem());

        entity.Patch(3);

        // Old curator (DataType="Logic", Type="ModuleCurator_F") should be removed.
        // New curators are added via CreateCuratorEntity which only sets raw strings, not DataType/Type properties.
        // So the entity list should have: 1 original non-playable + 3 new curators = 4 items
        entity.MissionEntityItems.Should().HaveCount(4);
        entity.MissionEntityItems.Count(x => x.DataType == "Logic" && x.Type == "ModuleCurator_F").Should().Be(0);
        entity.MissionEntityItems.Count(x => x.RawMissionEntityItem.Contains("type=\"ModuleCurator_F\";")).Should().Be(3);
    }

    [Fact]
    public void Patch_ShouldAddCuratorEntities_ForMaxCuratorsCount()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;
        SetupEmptyPatchData();

        var entity = new MissionEntity { ItemsCount = 0 };

        entity.Patch(5);

        var curators = entity.MissionEntityItems.Where(x => x.RawMissionEntityItem.Contains("type=\"ModuleCurator_F\";")).ToList();
        curators.Should().HaveCount(5);
    }

    [Fact]
    public void Patch_ShouldAddGroupsFromPatchData()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;

        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var unit = new MissionUnit
        {
            Callsign = "Alpha",
            SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" },
            Members =
            [
                new MissionPlayer
                {
                    Name = "Pte.Player",
                    ObjectClass = "UKSF_B_Rifleman",
                    Account = new DomainAccount { Id = "acc-1" },
                    Rank = ranks[0]
                }
            ]
        };
        unit.Members[0].Unit = unit;

        MissionPatchData.Instance = new MissionPatchData
        {
            OrderedUnits = [unit],
            Players = [],
            Ranks = ranks,
            Units = []
        };

        var entity = new MissionEntity { ItemsCount = 0 };

        entity.Patch(0);

        var groups = entity.MissionEntityItems.Where(x => x.RawMissionEntityItem.Contains("dataType=\"Group\";")).ToList();
        groups.Should().HaveCount(1);
    }

    [Fact]
    public void Patch_ShouldUpdateItemIndices()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;
        SetupEmptyPatchData();

        var entity = new MissionEntity { ItemsCount = 1 };
        entity.MissionEntityItems.Add(CreateNonPlayableItem());

        entity.Patch(2);

        entity.MissionEntityItems[0].RawMissionEntityItem[0].Should().Be("class Item0");
    }

    [Fact]
    public void Patch_ShouldUpdateItemsCount()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;
        SetupEmptyPatchData();

        var entity = new MissionEntity { ItemsCount = 1 };
        entity.MissionEntityItems.Add(CreateNonPlayableItem());

        entity.Patch(3);

        entity.ItemsCount.Should().Be(4); // 1 original + 3 curators
    }

    [Fact]
    public void Serialize_ShouldProduceCorrectFormat()
    {
        var entity = new MissionEntity { ItemsCount = 1 };
        entity.MissionEntityItems.Add(
            new MissionEntityItem
            {
                RawMissionEntityItem =
                [
                    "class Item0",
                    "{",
                    "dataType=\"Object\";",
                    "};"
                ],
                RawMissionEntities = []
            }
        );

        var result = entity.Serialize().ToList();

        result[0].Should().Be("class Entities");
        result[1].Should().Be("{");
        result[2].Should().Be("items = 1;");
        result.Last().Should().Be("};");
        result.Should().Contain("class Item0");
    }

    [Fact]
    public void Serialize_ShouldUpdateItemsCount()
    {
        var entity = new MissionEntity { ItemsCount = 5 }; // stale count
        entity.MissionEntityItems.Add(new MissionEntityItem { RawMissionEntityItem = ["class Item0", "{", "};"], RawMissionEntities = [] });
        entity.MissionEntityItems.Add(new MissionEntityItem { RawMissionEntityItem = ["class Item1", "{", "};"], RawMissionEntities = [] });

        var result = entity.Serialize().ToList();

        result.Should().Contain("items = 2;");
    }

    private static void SetupEmptyPatchData()
    {
        MissionPatchData.Instance = new MissionPatchData
        {
            OrderedUnits = [],
            Players = [],
            Ranks = [],
            Units = []
        };
    }

    private static MissionEntityItem CreatePlayableGroup()
    {
        var innerEntity = new MissionEntity { ItemsCount = 1 };
        innerEntity.MissionEntityItems.Add(
            new MissionEntityItem { IsPlayable = true, RawMissionEntityItem = ["class Item0", "{", "dataType=\"Object\";", "isPlayable=1;", "};"] }
        );
        return new MissionEntityItem
        {
            DataType = "Group",
            MissionEntity = innerEntity,
            RawMissionEntityItem = ["class Item0", "{", "dataType=\"Group\";", "};"]
        };
    }

    private static MissionEntityItem CreateIgnoredPlayableGroup()
    {
        var innerEntity = new MissionEntity { ItemsCount = 1 };
        innerEntity.MissionEntityItems.Add(
            new MissionEntityItem
            {
                IsPlayable = true,
                RawMissionEntityItem = ["class Item0", "{", "dataType=\"Object\";", "isPlayable=1;", "description=\"@ignore test\";", "};"]
            }
        );
        return new MissionEntityItem
        {
            DataType = "Group",
            MissionEntity = innerEntity,
            RawMissionEntityItem = ["class Item0", "{", "dataType=\"Group\";", "};"]
        };
    }

    private static MissionEntityItem CreateNonPlayableItem()
    {
        return new MissionEntityItem
        {
            DataType = "Object",
            IsPlayable = false,
            RawMissionEntityItem = ["class Item0", "{", "dataType=\"Object\";", "};"]
        };
    }
}
