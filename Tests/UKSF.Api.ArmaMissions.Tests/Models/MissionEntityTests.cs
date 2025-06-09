using FluentAssertions;
using System.Collections.Generic;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionEntityTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        // Act
        var subject = new MissionEntity();

        // Assert
        subject.ItemsCount.Should().Be(0);
        subject.MissionEntityItems.Should().NotBeNull();
        subject.MissionEntityItems.Should().BeEmpty();
    }

    [Fact]
    public void ShouldAllowSettingItemsCount()
    {
        // Arrange
        var subject = new MissionEntity();
        const int expectedCount = 5;

        // Act
        subject.ItemsCount = expectedCount;

        // Assert
        subject.ItemsCount.Should().Be(expectedCount);
    }

    [Fact]
    public void ShouldAllowAddingMissionEntityItems()
    {
        // Arrange
        var subject = new MissionEntity();
        var item = new MissionEntityItem { Type = "TestType", DataType = "TestData" };

        // Act
        subject.MissionEntityItems.Add(item);

        // Assert
        subject.MissionEntityItems.Should().HaveCount(1);
        subject.MissionEntityItems[0].Should().Be(item);
    }

    [Fact]
    public void MissionEntityItems_ShouldUseModernCollectionInitialization()
    {
        // Arrange & Act
        var subject = new MissionEntity();

        // Assert - Verify the collection is properly initialized as List<T>
        subject.MissionEntityItems.Should().NotBeNull();
        subject.MissionEntityItems.Should().BeOfType<List<MissionEntityItem>>();
    }
}
