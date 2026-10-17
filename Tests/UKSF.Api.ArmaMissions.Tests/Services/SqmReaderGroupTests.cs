using FluentAssertions;
using UKSF.Api.ArmaMissions.Models.Sqm;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmReaderGroupTests : SqmReaderTestBase
{
    [Fact]
    public void Read_WhenEntityIsGroup_ParsesAsSqmGroupWithChildren()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Group";
            class Entities
            {
            items=2;
            class Item0
            {
            dataType="Object";
            type="UKSF_B_Rifleman";
            isPlayable=1;
            };
            class Item1
            {
            dataType="Object";
            type="UKSF_B_Medic";
            isPlayable=1;
            };
            };
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.Sqm.Entities.Should().ContainSingle().Which.Should().BeOfType<SqmGroup>();
        var group = (SqmGroup)context.Sqm.Entities[0];
        group.Children.Should().HaveCount(2);
        group.Children.Should().AllBeOfType<SqmObject>();
    }

    [Fact]
    public void Read_WhenAllGroupChildrenArePlayable_SetsAllChildrenPlayableTrue()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Group";
            class Entities
            {
            items=2;
            class Item0
            {
            dataType="Object";
            type="UKSF_B_Rifleman";
            isPlayable=1;
            };
            class Item1
            {
            dataType="Object";
            type="UKSF_B_Medic";
            isPlayable=1;
            };
            };
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var group = (SqmGroup)context.Sqm.Entities[0];
        group.AllChildrenPlayable.Should().BeTrue();
    }

    [Fact]
    public void Read_WhenGroupHasNonPlayableChild_SetsAllChildrenPlayableFalse()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Group";
            class Entities
            {
            items=2;
            class Item0
            {
            dataType="Object";
            type="UKSF_B_Rifleman";
            isPlayable=1;
            };
            class Item1
            {
            dataType="Object";
            type="B_Soldier_F";
            isPlayable=0;
            };
            };
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var group = (SqmGroup)context.Sqm.Entities[0];
        group.AllChildrenPlayable.Should().BeFalse();
    }

    [Fact]
    public void Read_WhenGroupHasNoChildren_SetsAllChildrenPlayableFalse()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Group";
            class Entities
            {
            items=0;
            };
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var group = (SqmGroup)context.Sqm.Entities[0];
        group.AllChildrenPlayable.Should().BeFalse();
    }

    [Fact]
    public void Read_WhenGroupChildContainsIgnoreTag_SetsIsIgnoredTrue()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Group";
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Object";
            type="UKSF_B_Rifleman@ignore";
            isPlayable=1;
            };
            };
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var group = (SqmGroup)context.Sqm.Entities[0];
        group.IsIgnored.Should().BeTrue();
    }

    [Fact]
    public void Read_WhenNoGroupChildContainsIgnoreTag_LeavesIsIgnoredFalse()
    {
        WriteSqm(
            """
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Group";
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
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        var group = (SqmGroup)context.Sqm.Entities[0];
        group.IsIgnored.Should().BeFalse();
    }
}
