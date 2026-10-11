using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Admin;

public class VariablesServiceForcedFeatureTests
{
    private readonly Mock<IVariablesContext> _context = new();

    private VariablesService CreateSubject(string? disabledFeatures, string? forcedOnFeatures, bool verifyMode)
    {
        return new VariablesService(_context.Object, new FeatureOverrides(disabledFeatures, forcedOnFeatures, verifyMode));
    }

    private void GivenStoredFeature(string featureKey, bool enabled)
    {
        _context.Setup(x => x.GetSingle($"FEATURE_{featureKey}")).Returns(new DomainVariableItem { Key = $"FEATURE_{featureKey}", Item = enabled });
    }

    [Fact]
    public void GetFeatureState_ReturnsTrue_WhenNotificationsAreForcedOnInVerifyMode()
    {
        GivenStoredFeature("NOTIFICATIONS", false);

        CreateSubject(null, "notifications", true).GetFeatureState("NOTIFICATIONS").Should().BeTrue();
    }

    [Fact]
    public void GetFeatureState_IgnoresForceOn_OutsideVerifyMode()
    {
        GivenStoredFeature("NOTIFICATIONS", false);

        var subject = CreateSubject(null, "NOTIFICATIONS", false);

        subject.GetFeatureState("NOTIFICATIONS").Should().BeFalse();
        new FeatureOverrides(null, "NOTIFICATIONS", false).ForcedOn.Should().BeEmpty();
    }

    [Fact]
    public void GetFeatureState_NeverForcesIntegrationsOn_EvenInVerifyMode()
    {
        GivenStoredFeature("TEAMSPEAK", false);
        GivenStoredFeature("DISCORD", false);
        GivenStoredFeature("NOTIFICATIONS", false);

        var subject = CreateSubject(null, "TEAMSPEAK,DISCORD,NOTIFICATIONS", true);

        subject.GetFeatureState("TEAMSPEAK").Should().BeFalse();
        subject.GetFeatureState("DISCORD").Should().BeFalse();
        subject.GetFeatureState("NOTIFICATIONS").Should().BeTrue();
    }

    [Fact]
    public void GetFeatureState_LetsDisabledOverrideForceOn()
    {
        GivenStoredFeature("NOTIFICATIONS", true);

        CreateSubject("NOTIFICATIONS", "NOTIFICATIONS", true).GetFeatureState("NOTIFICATIONS").Should().BeFalse();
    }

    [Fact]
    public void GetFeatureState_KeepsStoredState_ForFeaturesNotForcedOn()
    {
        GivenStoredFeature("NPC_BROKER", true);
        GivenStoredFeature("DISCORD", false);

        var subject = CreateSubject(null, "NOTIFICATIONS", true);

        subject.GetFeatureState("NPC_BROKER").Should().BeTrue();
        subject.GetFeatureState("DISCORD").Should().BeFalse();
    }

    [Theory]
    [InlineData("TEAMSPEAK")]
    [InlineData("DISCORD")]
    public void GetFeatureState_ReturnsFalse_ForIntegrationStoredTrueInVerifyMode(string featureKey)
    {
        GivenStoredFeature(featureKey, true);

        CreateSubject(null, "NOTIFICATIONS", true).GetFeatureState(featureKey).Should().BeFalse();
    }

    [Theory]
    [InlineData("TEAMSPEAK")]
    [InlineData("DISCORD")]
    public void GetFeatureState_ReturnsTrue_ForIntegrationStoredTrueOutsideVerifyMode(string featureKey)
    {
        GivenStoredFeature(featureKey, true);

        CreateSubject(null, null, false).GetFeatureState(featureKey).Should().BeTrue();
    }
}
