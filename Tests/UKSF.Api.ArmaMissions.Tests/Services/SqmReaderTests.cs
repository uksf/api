using FluentAssertions;
using UKSF.Api.ArmaMissions.Models.Sqm;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmReaderTests : SqmReaderTestBase
{
    [Fact]
    public void Read_WhenFileHasNoEntitiesBlock_ReadsNoEntities()
    {
        WriteSqm(
            """
            version=53;
            someKey="someValue";
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.Sqm.EntitiesNode.Should().BeNull();
        context.Sqm.Entities.Should().BeEmpty();
    }

    [Fact]
    public void Read_WhenMissionHasEntities_PrefersThemOverEarlierEntitiesBlocks()
    {
        WriteSqm(
            """
            class OutroLoop
            {
            class Entities
            {
            items=0;
            };
            };
            class Mission
            {
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Logic";
            type="ModuleCurator_F";
            };
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.Sqm.Entities.Should().ContainSingle().Which.Should().BeOfType<SqmLogic>().Which.Type.Should().Be("ModuleCurator_F");
    }

    [Fact]
    public void Read_WhenItemIDProviderPresent_SetsNextEntityId()
    {
        WriteSqm(
            """
            class ItemIDProvider
            {
            nextID=42;
            };
            class Entities
            {
            items=0;
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.NextEntityId.Should().Be(42);
    }

    [Fact]
    public void Read_WhenItemIDProviderAbsent_LeavesNextEntityIdAtZero()
    {
        WriteSqm(
            """
            version=53;
            class Entities
            {
            items=0;
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.NextEntityId.Should().Be(0);
    }

    [Fact]
    public void Read_WhenItemIDProviderIsBelowAnExistingId_SetsNextEntityIdAboveHighestId()
    {
        WriteSqm(
            """
            class EditorData
            {
            class ItemIDProvider
            {
            nextID=3;
            };
            };
            class Mission
            {
            class Entities
            {
            items=1;
            class Item0
            {
            dataType="Logic";
            id=17;
            };
            };
            };
            """
        );
        var context = CreateContext();

        Subject.Read(context);

        context.NextEntityId.Should().Be(18);
    }
}
