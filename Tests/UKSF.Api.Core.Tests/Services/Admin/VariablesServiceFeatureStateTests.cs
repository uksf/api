using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Admin;

public class VariablesServiceFeatureStateTests
{
    private readonly Mock<IVariablesContext> _context = new();

    private VariablesService CreateSubject(string? disabledFeatures)
    {
        return new VariablesService(_context.Object, new FeatureOverrides(disabledFeatures));
    }

    private void GivenStoredFeature(string featureKey, bool enabled)
    {
        _context.Setup(x => x.GetSingle($"FEATURE_{featureKey}")).Returns(new DomainVariableItem { Key = $"FEATURE_{featureKey}", Item = enabled });
    }

    [Fact]
    public void GetFeatureState_ReturnsStoredState_WhenNoOverride()
    {
        GivenStoredFeature("DISCORD", true);

        CreateSubject(null).GetFeatureState("DISCORD").Should().BeTrue();
    }

    [Fact]
    public void GetFeatureState_ReturnsFalse_WhenFeatureIsDisabledByOverride()
    {
        GivenStoredFeature("DISCORD", true);
        GivenStoredFeature("TEAMSPEAK", true);

        var subject = CreateSubject("DISCORD, teamspeak");

        subject.GetFeatureState("DISCORD").Should().BeFalse();
        subject.GetFeatureState("TEAMSPEAK").Should().BeFalse();
    }

    [Fact]
    public void GetFeatureState_KeepsStoredState_ForFeaturesNotInOverride()
    {
        GivenStoredFeature("NPC_BROKER", true);

        CreateSubject("DISCORD").GetFeatureState("NPC_BROKER").Should().BeTrue();
    }

    [Fact]
    public void GetFeatureState_NeverEnablesAFeature()
    {
        GivenStoredFeature("DISCORD", false);

        CreateSubject("TEAMSPEAK").GetFeatureState("DISCORD").Should().BeFalse();
    }

    [Fact]
    public void FeatureOverrides_ListsDisabledFeaturesInUpperCase()
    {
        new FeatureOverrides(" discord ,,Teamspeak ").Disabled.Should().BeEquivalentTo(["DISCORD", "TEAMSPEAK"]);
    }

    [Fact]
    public void FeatureOverrides_IsEmpty_WhenUnset()
    {
        new FeatureOverrides(null).Disabled.Should().BeEmpty();
        new FeatureOverrides("").Disabled.Should().BeEmpty();
    }
}
