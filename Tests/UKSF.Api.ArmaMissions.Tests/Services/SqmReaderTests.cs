using System;
using System.IO;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.ArmaMissions.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmReaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqmReader _subject = new();

    public SqmReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_sqmreader_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private MissionPatchContext CreateContext() => new() { FolderPath = _tempDir };

    private void WriteSqm(string content) => File.WriteAllText(Path.Combine(_tempDir, "mission.sqm"), content);

    // ─── Header / Footer ──────────────────────────────────────────────────

    [Fact]
    public void Read_WhenFileHasNoEntitiesBlock_PopulatesOnlyHeaderLines()
    {
        WriteSqm(
            """
            version=53;
            someKey="someValue";
            """
        );
        var context = CreateContext();

        _subject.Read(context);

        context.Sqm.HeaderLines.Should().Contain("version=53;");
        context.Sqm.HeaderLines.Should().Contain("someKey=\"someValue\";");
        context.Sqm.Entities.Should().BeEmpty();
        context.Sqm.FooterLines.Should().BeEmpty();
    }

    [Fact]
    public void Read_WhenFileHasEntitiesBlock_SplitsHeaderAndFooterCorrectly()
    {
        WriteSqm(
            """
            version=53;
            class Entities
            {
            items=0;
            };
            trailingKey="trailingValue";
            """
        );
        var context = CreateContext();

        _subject.Read(context);

        context.Sqm.HeaderLines.Should().Contain("version=53;");
        context.Sqm.HeaderLines.Should().NotContain(l => l.Contains("class Entities"));
        context.Sqm.FooterLines.Should().Contain("trailingKey=\"trailingValue\";");
    }

    // ─── NextEntityId ─────────────────────────────────────────────────────

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

        _subject.Read(context);

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

        _subject.Read(context);

        context.NextEntityId.Should().Be(0);
    }

    // ─── Unbin Header Stripping ───────────────────────────────────────────

    [Fact]
    public void Read_WhenUnbinHeaderPresent_StripsFirst7Lines()
    {
        WriteSqm(
            """
            ////////////////////////////////////////////////////////////////////
            // Line 2
            // Line 3
            // Line 4
            // Line 5
            // Line 6
            // Line 7
            version=53;
            class Entities
            {
            items=0;
            };
            """
        );
        var context = CreateContext();

        _subject.Read(context);

        context.Sqm.HeaderLines.Should().Contain("version=53;");
        context.Sqm.HeaderLines.Should().NotContain(l => l.StartsWith("////"));
    }

    // ─── Object Entities ─────────────────────────────────────────────────

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

        _subject.Read(context);

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

        _subject.Read(context);

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

        _subject.Read(context);

        var obj = (SqmObject)context.Sqm.Entities[0];
        obj.IsPlayable.Should().BeTrue();
    }

    // ─── Logic Entities ───────────────────────────────────────────────────

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

        _subject.Read(context);

        context.Sqm.Entities.Should().ContainSingle().Which.Should().BeOfType<SqmLogic>();
        var logic = (SqmLogic)context.Sqm.Entities[0];
        logic.Type.Should().Be("ModuleEndMission_F");
    }

    // ─── Passthrough Entities ─────────────────────────────────────────────

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

        _subject.Read(context);

        context.Sqm.Entities.Should().ContainSingle().Which.Should().BeOfType<SqmPassthrough>();
    }

    // ─── Group Entities ───────────────────────────────────────────────────

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

        _subject.Read(context);

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

        _subject.Read(context);

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

        _subject.Read(context);

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

        _subject.Read(context);

        var group = (SqmGroup)context.Sqm.Entities[0];
        group.AllChildrenPlayable.Should().BeFalse();
    }

    // ─── @ignore Tag ──────────────────────────────────────────────────────

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

        _subject.Read(context);

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

        _subject.Read(context);

        var group = (SqmGroup)context.Sqm.Entities[0];
        group.IsIgnored.Should().BeFalse();
    }

    // ─── RawLines ─────────────────────────────────────────────────────────

    [Fact]
    public void Read_ParsedEntity_PopulatesRawLines()
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

        _subject.Read(context);

        var entity = context.Sqm.Entities[0];
        entity.RawLines.Should().NotBeEmpty();
        entity.RawLines.Should().Contain(l => l.Contains("dataType"));
    }

    // ─── Multiple Entities ────────────────────────────────────────────────

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

        _subject.Read(context);

        context.Sqm.Entities.Should().HaveCount(3);
        context.Sqm.Entities[0].Should().BeOfType<SqmGroup>();
        context.Sqm.Entities[1].Should().BeOfType<SqmObject>();
        context.Sqm.Entities[2].Should().BeOfType<SqmLogic>();
    }
}
