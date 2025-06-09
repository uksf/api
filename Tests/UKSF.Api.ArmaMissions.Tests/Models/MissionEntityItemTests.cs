using FluentAssertions;
using System.Collections.Generic;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionEntityItemTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        // Act
        var subject = new MissionEntityItem();

        // Assert
        subject.DataType.Should().BeNull();
        subject.IsPlayable.Should().BeFalse();
        subject.MissionEntity.Should().BeNull();
        subject.Type.Should().BeNull();
        subject.RawMissionEntities.Should().NotBeNull();
        subject.RawMissionEntities.Should().BeEmpty();
        subject.RawMissionEntityItem.Should().NotBeNull();
        subject.RawMissionEntityItem.Should().BeEmpty();
    }

    [Fact]
    public void ShouldAllowSettingDataType()
    {
        // Arrange
        var subject = new MissionEntityItem();
        const string expectedDataType = "TestDataType";

        // Act
        subject.DataType = expectedDataType;

        // Assert
        subject.DataType.Should().Be(expectedDataType);
    }

    [Fact]
    public void ShouldAllowSettingIsPlayable()
    {
        // Arrange
        var subject = new MissionEntityItem();

        // Act
        subject.IsPlayable = true;

        // Assert
        subject.IsPlayable.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowSettingType()
    {
        // Arrange
        var subject = new MissionEntityItem();
        const string expectedType = "TestType";

        // Act
        subject.Type = expectedType;

        // Assert
        subject.Type.Should().Be(expectedType);
    }

    [Fact]
    public void ShouldAllowSettingMissionEntity()
    {
        // Arrange
        var subject = new MissionEntityItem();
        var missionEntity = new MissionEntity { ItemsCount = 5 };

        // Act
        subject.MissionEntity = missionEntity;

        // Assert
        subject.MissionEntity.Should().Be(missionEntity);
        subject.MissionEntity.ItemsCount.Should().Be(5);
    }

    [Fact]
    public void RawMissionEntities_ShouldUseModernCollectionInitialization()
    {
        // Arrange & Act
        var subject = new MissionEntityItem();

        // Assert
        subject.RawMissionEntities.Should().NotBeNull();
        subject.RawMissionEntities.Should().BeOfType<List<string>>();
    }

    [Fact]
    public void RawMissionEntityItem_ShouldUseModernCollectionInitialization()
    {
        // Arrange & Act
        var subject = new MissionEntityItem();

        // Assert
        subject.RawMissionEntityItem.Should().NotBeNull();
        subject.RawMissionEntityItem.Should().BeOfType<List<string>>();
    }

    [Fact]
    public void ShouldAllowAddingToRawMissionEntities()
    {
        // Arrange
        var subject = new MissionEntityItem();
        const string testEntity = "TestEntity";

        // Act
        subject.RawMissionEntities.Add(testEntity);

        // Assert
        subject.RawMissionEntities.Should().HaveCount(1);
        subject.RawMissionEntities[0].Should().Be(testEntity);
    }

    [Fact]
    public void ShouldAllowAddingToRawMissionEntityItem()
    {
        // Arrange
        var subject = new MissionEntityItem();
        const string testItem = "TestItem";

        // Act
        subject.RawMissionEntityItem.Add(testItem);

        // Assert
        subject.RawMissionEntityItem.Should().HaveCount(1);
        subject.RawMissionEntityItem[0].Should().Be(testItem);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(0.5)]
    [InlineData(999.99)]
    public void Position_ShouldAllowSettingStaticValue(double expectedPosition)
    {
        // Act
        MissionEntityItem.Position = expectedPosition;

        // Assert
        MissionEntityItem.Position.Should().Be(expectedPosition);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void CuratorPosition_ShouldAllowSettingStaticValue(double expectedPosition)
    {
        // Act
        MissionEntityItem.CuratorPosition = expectedPosition;

        // Assert
        MissionEntityItem.CuratorPosition.Should().Be(expectedPosition);
    }
}
