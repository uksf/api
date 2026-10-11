namespace UKSF.Api.Core.Services;

public sealed class FeatureOverrides(string? disabledFeatures, string? forcedOnFeatures = null, bool verifyMode = false)
{
    public const string EnvironmentKey = "UKSF_FEATURE_OFF";
    public const string ForceOnEnvironmentKey = "UKSF_FEATURE_ON";

    private static readonly IReadOnlySet<string> ForceableInVerifyMode = new HashSet<string> { "NOTIFICATIONS" };

    public IReadOnlySet<string> Disabled { get; } = Parse(disabledFeatures);

    public IReadOnlySet<string> ForcedOn { get; } = verifyMode ? Parse(forcedOnFeatures).Where(ForceableInVerifyMode.Contains).ToHashSet() : new HashSet<string>();

    public static FeatureOverrides FromEnvironment()
    {
        return new FeatureOverrides(
            Environment.GetEnvironmentVariable(EnvironmentKey),
            Environment.GetEnvironmentVariable(ForceOnEnvironmentKey),
            VerifyMode.FromEnvironment().Enabled
        );
    }

    public bool IsDisabled(string featureKey)
    {
        return Disabled.Contains(featureKey.ToUpperInvariant());
    }

    public bool IsForcedOn(string featureKey)
    {
        return ForcedOn.Contains(featureKey.ToUpperInvariant());
    }

    private static HashSet<string> Parse(string? features)
    {
        return (features ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(feature => feature.ToUpperInvariant()).ToHashSet();
    }
}
