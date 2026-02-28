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
public class MissionEntityItemHelperTests : IDisposable
{
    public MissionEntityItemHelperTests()
    {
        Mission.NextId = 100;
        MissionEntityItem.Position = 10;
        MissionEntityItem.CuratorPosition = 0.5;
    }

    public void Dispose()
    {
        MissionPatchData.Instance = null;
    }

    [Fact]
    public void CreateFromList_ShouldParseObjectType()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Object\";",
            "id=1;",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.DataType.Should().Be("Object");
        result.RawMissionEntityItem.Should().BeEquivalentTo(rawItem);
        result.IsPlayable.Should().BeFalse();
    }

    [Fact]
    public void CreateFromList_ShouldDetectPlayableObject_WhenIsPlayableEquals1()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Object\";",
            "isPlayable=1;",
            "id=1;",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.DataType.Should().Be("Object");
        result.IsPlayable.Should().BeTrue();
    }

    [Fact]
    public void CreateFromList_ShouldDetectPlayableObject_WhenIsPlayerEquals1()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Object\";",
            "isPlayer=1;",
            "id=1;",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.DataType.Should().Be("Object");
        result.IsPlayable.Should().BeTrue();
    }

    [Fact]
    public void CreateFromList_ShouldNotBePlayable_WhenIsPlayableEquals0()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Object\";",
            "isPlayable=0;",
            "id=1;",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.IsPlayable.Should().BeFalse();
    }

    [Fact]
    public void CreateFromList_ShouldParseGroupType_WithNestedEntities()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Group\";",
            "class Entities",
            "{",
            "items = 1;",
            "class Item0",
            "{",
            "dataType=\"Object\";",
            "id=1;",
            "};",
            "};",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.DataType.Should().Be("Group");
        result.MissionEntity.Should().NotBeNull();
        result.MissionEntity.ItemsCount.Should().Be(1);
        result.RawMissionEntities.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateFromList_ShouldParseGroupType_WithNoEntitiesBlock()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Group\";",
            "id=1;",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.DataType.Should().Be("Group");
        result.MissionEntity.Should().BeNull();
        result.RawMissionEntities.Count.Should().Be(0);
    }

    [Fact]
    public void CreateFromList_ShouldParseLogicType_WithType()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Logic\";",
            "type=\"ModuleCurator_F\";",
            "id=1;",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.DataType.Should().Be("Logic");
        result.Type.Should().Be("ModuleCurator_F");
    }

    [Fact]
    public void CreateFromList_ShouldParseLogicType_WithoutType()
    {
        var rawItem = new List<string>
        {
            "class Item0",
            "{",
            "dataType=\"Logic\";",
            "id=1;",
            "};"
        };

        var result = MissionEntityItemHelper.CreateFromList(rawItem);

        result.DataType.Should().Be("Logic");
        result.Type.Should().BeNull();
    }

    [Fact]
    public void CreateFromPlayer_ShouldCreateEntityWithCorrectStructure()
    {
        var unit = new MissionUnit { Callsign = "Alpha", SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" } };
        var player = new MissionPlayer
        {
            Name = "Cpl.TestPlayer",
            ObjectClass = "UKSF_B_Rifleman",
            Account = new DomainAccount { Id = "acc-1", RoleAssignment = "" },
            Unit = unit
        };

        var result = MissionEntityItemHelper.CreateFromPlayer(player, 0);

        result.RawMissionEntityItem.Should().Contain("dataType=\"Object\";");
        result.RawMissionEntityItem.Should().Contain("flags=7;");
        result.RawMissionEntityItem.Should().Contain("side=\"West\";");
        result.RawMissionEntityItem.Should().Contain("type=\"UKSF_B_Rifleman\";");
        result.RawMissionEntityItem.Should().Contain("isPlayable=1;");
        result.RawMissionEntityItem.Should().Contain(x => x.Contains("description=\"Cpl.TestPlayer@Alpha\";"));
    }

    [Fact]
    public void CreateFromPlayer_ShouldUseFlags5_WhenNotFirstItem()
    {
        var unit = new MissionUnit { Callsign = "Alpha", SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" } };
        var player = new MissionPlayer
        {
            Name = "Pte.Player2",
            ObjectClass = "UKSF_B_Rifleman",
            Account = new DomainAccount { Id = "acc-2" },
            Unit = unit
        };

        var result = MissionEntityItemHelper.CreateFromPlayer(player, 1);

        result.RawMissionEntityItem.Should().Contain("flags=5;");
    }

    [Fact]
    public void CreateFromPlayer_ShouldIncludeRoleAssignment_WhenPresent()
    {
        var unit = new MissionUnit { Callsign = "Alpha", SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" } };
        var player = new MissionPlayer
        {
            Name = "Sgt.Leader",
            ObjectClass = "UKSF_B_SectionLeader",
            Account = new DomainAccount { Id = "acc-3", RoleAssignment = "1 Section Commander" },
            Unit = unit
        };

        var result = MissionEntityItemHelper.CreateFromPlayer(player, 0);

        result.RawMissionEntityItem.Should().Contain(x => x.Contains("description=\"Sgt.Leader - 1 Section Commander@Alpha\";"));
    }

    [Fact]
    public void CreateFromPlayer_ShouldAddEngineerTrait_WhenPlayerIsEngineer()
    {
        var unit = new MissionUnit { Callsign = "Alpha", SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" } };
        var player = new MissionPlayer
        {
            Name = "Pte.Engineer",
            ObjectClass = "UKSF_B_Rifleman",
            Account = new DomainAccount { Id = "acc-4", Qualifications = new Qualifications { Engineer = true } },
            Unit = unit
        };

        var result = MissionEntityItemHelper.CreateFromPlayer(player, 0);

        result.RawMissionEntityItem.Should().Contain("property=\"Enh_unitTraits_engineer\";");
        result.RawMissionEntityItem.Should().Contain("expression=\"_this setUnitTrait ['Engineer',_value]\";");
        result.RawMissionEntityItem.Should().Contain("value=1;");
    }

    [Fact]
    public void CreateFromPlayer_ShouldNotAddEngineerTrait_WhenPlayerIsNotEngineer()
    {
        var unit = new MissionUnit { Callsign = "Alpha", SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" } };
        var player = new MissionPlayer
        {
            Name = "Pte.Regular",
            ObjectClass = "UKSF_B_Rifleman",
            Account = new DomainAccount { Id = "acc-5", Qualifications = new Qualifications { Engineer = false } },
            Unit = unit
        };

        var result = MissionEntityItemHelper.CreateFromPlayer(player, 0);

        result.RawMissionEntityItem.Should().NotContain("property=\"Enh_unitTraits_engineer\";");
    }

    [Fact]
    public void CreateFromPlayer_ShouldIncrementPosition()
    {
        MissionEntityItem.Position = 10;
        var unit = new MissionUnit { Callsign = "Alpha", SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" } };
        var player = new MissionPlayer
        {
            Name = "Player",
            ObjectClass = "UKSF_B_Rifleman",
            Account = new DomainAccount { Id = "acc-1" },
            Unit = unit
        };

        MissionEntityItemHelper.CreateFromPlayer(player, 0);
        var secondResult = MissionEntityItemHelper.CreateFromPlayer(player, 1);

        secondResult.RawMissionEntityItem.Should().Contain(x => x.Contains("position[]={11,0,0};"));
    }

    [Fact]
    public void CreateFromPlayer_ShouldIncrementNextId()
    {
        Mission.NextId = 100;
        var unit = new MissionUnit { Callsign = "Alpha", SourceUnit = new DomainUnit { Id = "test-unit", Callsign = "Alpha" } };
        var player = new MissionPlayer
        {
            Name = "Player",
            ObjectClass = "UKSF_B_Rifleman",
            Account = new DomainAccount { Id = "acc-1" },
            Unit = unit
        };

        var result = MissionEntityItemHelper.CreateFromPlayer(player, 0);

        result.RawMissionEntityItem.Should().Contain("id=100;");
        Mission.NextId.Should().Be(101);
    }

    [Fact]
    public void CreateFromPlayer_ShouldResolvePilotCallsign_WhenUnitIsPilot()
    {
        var unit = new MissionUnit { Callsign = "Original", SourceUnit = new DomainUnit { Id = "5a435eea905d47336442c75a", Callsign = "Original" } };
        var player = new MissionPlayer
        {
            Name = "Pilot",
            ObjectClass = "UKSF_B_Pilot",
            Account = new DomainAccount { Id = "acc-1" },
            Unit = unit
        };

        var result = MissionEntityItemHelper.CreateFromPlayer(player, 0);

        result.RawMissionEntityItem.Should().Contain(x => x.Contains("@JSFAW"));
    }

    [Fact]
    public void CreateFromMissionEntity_ShouldCreateGroupEntity_WithCallsign()
    {
        var innerEntity = new MissionEntity();
        innerEntity.MissionEntityItems.Add(new MissionEntityItem { RawMissionEntityItem = ["class Item0", "{", "dataType=\"Object\";", "};"] });

        var result = MissionEntityItemHelper.CreateFromMissionEntity(innerEntity, "Bravo");

        result.MissionEntity.Should().BeSameAs(innerEntity);
        result.RawMissionEntityItem.Should().Contain("dataType=\"Group\";");
        result.RawMissionEntityItem.Should().Contain("side=\"West\";");
        result.RawMissionEntityItem.Should().Contain("property=\"groupID\";");
        result.RawMissionEntityItem.Should().Contain("value=\"Bravo\";");
        result.RawMissionEntityItem.Should().Contain("expression=\"[_this, _value] call CBA_fnc_setCallsign\";");
    }

    [Fact]
    public void CreateFromMissionEntity_ShouldIncrementNextId()
    {
        Mission.NextId = 200;
        var innerEntity = new MissionEntity();

        var result = MissionEntityItemHelper.CreateFromMissionEntity(innerEntity, "Test");

        result.RawMissionEntityItem.Should().Contain("id=200;");
        Mission.NextId.Should().Be(201);
    }

    [Fact]
    public void CreateCuratorEntity_ShouldCreateLogicEntity()
    {
        var result = MissionEntityItemHelper.CreateCuratorEntity();

        result.RawMissionEntityItem.Should().Contain("dataType=\"Logic\";");
        result.RawMissionEntityItem.Should().Contain("type=\"ModuleCurator_F\";");
        result.RawMissionEntityItem.Should().Contain("class CustomAttributes");
    }

    [Fact]
    public void CreateCuratorEntity_ShouldIncrementCuratorPosition()
    {
        MissionEntityItem.CuratorPosition = 0.5;

        MissionEntityItemHelper.CreateCuratorEntity();
        var second = MissionEntityItemHelper.CreateCuratorEntity();

        second.RawMissionEntityItem.Should().Contain(x => x.Contains("position[]={1.5,0,0.25};"));
    }

    [Fact]
    public void CreateCuratorEntity_ShouldIncrementNextId()
    {
        Mission.NextId = 300;

        var result = MissionEntityItemHelper.CreateCuratorEntity();

        result.RawMissionEntityItem.Should().Contain("id=300;");
        Mission.NextId.Should().Be(301);
    }

    [Fact]
    public void Ignored_ShouldReturnTrue_WhenRawItemContainsIgnoreTag()
    {
        var item = new MissionEntityItem
        {
            RawMissionEntityItem =
            [
                "class Item0",
                "{",
                "description=\"@ignore test\";",
                "};"
            ]
        };

        var result = item.Ignored();

        result.Should().BeTrue();
    }

    [Fact]
    public void Ignored_ShouldReturnTrue_WhenIgnoreTagIsUpperCase()
    {
        var item = new MissionEntityItem
        {
            RawMissionEntityItem =
            [
                "class Item0",
                "{",
                "description=\"@IGNORE test\";",
                "};"
            ]
        };

        var result = item.Ignored();

        result.Should().BeTrue();
    }

    [Fact]
    public void Ignored_ShouldReturnFalse_WhenNoIgnoreTag()
    {
        var item = new MissionEntityItem
        {
            RawMissionEntityItem =
            [
                "class Item0",
                "{",
                "description=\"normal entity\";",
                "};"
            ]
        };

        var result = item.Ignored();

        result.Should().BeFalse();
    }

    [Fact]
    public void Patch_ShouldUpdateClassItemIndex()
    {
        var item = new MissionEntityItem
        {
            RawMissionEntityItem =
            [
                "class Item0",
                "{",
                "dataType=\"Object\";",
                "};"
            ]
        };

        item.Patch(5);

        item.RawMissionEntityItem[0].Should().Be("class Item5");
    }

    [Fact]
    public void Serialize_ShouldReturnRawItems_WhenNoEntities()
    {
        var item = new MissionEntityItem
        {
            RawMissionEntityItem =
            [
                "class Item0",
                "{",
                "dataType=\"Object\";",
                "};"
            ],
            RawMissionEntities = []
        };

        var result = item.Serialize().ToList();

        result.Should().BeEquivalentTo(item.RawMissionEntityItem);
    }

    [Fact]
    public void Serialize_ShouldInlineSerializedEntities_WhenGroupHasEntities()
    {
        var innerEntity = new MissionEntity { ItemsCount = 1 };
        innerEntity.MissionEntityItems.Add(
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

        var groupItem = new MissionEntityItem
        {
            MissionEntity = innerEntity,
            RawMissionEntityItem =
            [
                "class Item0",
                "{",
                "dataType=\"Group\";",
                "class Entities",
                "{",
                "items = 0;",
                "};",
                "};"
            ],
            RawMissionEntities =
            [
                "class Entities",
                "{",
                "items = 0;",
                "};"
            ]
        };

        var result = groupItem.Serialize().ToList();

        result.Should().Contain("items = 1;");
        result.Should().Contain("class Item0");
    }
}
