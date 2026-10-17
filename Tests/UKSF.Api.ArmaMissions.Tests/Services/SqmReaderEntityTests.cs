using FluentAssertions;
using UKSF.Api.ArmaMissions.Models.Sqm;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmReaderEntityTests : SqmReaderTestBase
{
    [Fact]
    public void Read_WhenEntityIsObject_ParsesAsSqmObject()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Object";
            type="B_Soldier_F";
            isPlayable=0;
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.Sqm.Entities.Should().ContainSingle().Which.Should().BeOfType<SqmObject>();
        var obj = (SqmObject)context.Sqm.Entities[0];
        obj.Type.Should().Be("B_Soldier_F");
        obj.IsPlayable.Should().BeFalse();
    }

    [Fact]
    public void Read_WhenObjectHasIsPlayable1_SetsIsPlayableTrue()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Object";
            type="UKSF_B_Rifleman";
            isPlayable=1;
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var obj = (SqmObject)context.Sqm.Entities[0];
        obj.IsPlayable.Should().BeTrue();
    }

    [Fact]
    public void Read_WhenObjectHasIsPlayer1_SetsIsPlayableTrue()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Object";
            type="UKSF_B_Rifleman";
            isPlayer=1;
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var obj = (SqmObject)context.Sqm.Entities[0];
        obj.IsPlayable.Should().BeTrue();
    }

    [Fact]
    public void Read_WhenEntityIsLogic_ParsesAsSqmLogic()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Logic";
            type="ModuleEndMission_F";
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.Sqm.Entities.Should().ContainSingle().Which.Should().BeOfType<SqmLogic>();
        var logic = (SqmLogic)context.Sqm.Entities[0];
        logic.Type.Should().Be("ModuleEndMission_F");
    }

    [Fact]
    public void Read_WhenEntityDataTypeIsUnknown_ParsesAsSqmPassthrough()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Marker";
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.Sqm.Entities.Should().ContainSingle().Which.Should().BeOfType<SqmPassthrough>();
    }

    [Fact]
    public void Read_ParsedEntity_KeepsItsSourceNode()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Object";
            type="B_Soldier_F";
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var entity = context.Sqm.Entities[0];
        entity.Node.Name.Should().Be("Item0");
        entity.Node.Value("type").Should().Be("B_Soldier_F");
    }

    [Fact]
    public void Read_WhenFileHasMultipleEntities_ParsesAllEntities()
    {
        WriteSqm(
            """
            class Entities
            {
            items=3;
            class Item0
            {
            dataType="Group";
            class Entities
            {
            items=0;
            };
            };
            class Item1
            {
            dataType="Object";
            type="B_Soldier_F";
            };
            class Item2
            {
            dataType="Logic";
            type="ModuleEndMission_F";
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.Sqm.Entities.Should().HaveCount(3);
        context.Sqm.Entities[0].Should().BeOfType<SqmGroup>();
        context.Sqm.Entities[1].Should().BeOfType<SqmObject>();
        context.Sqm.Entities[2].Should().BeOfType<SqmLogic>();
    }
}
