namespace UKSF.Api.Core.Services;

public sealed class FeatureOverrides(string? disabledFeatures)
{
    public const string EnvironmentKey = "UKSF_FEATURE_OFF";

    public IReadOnlySet<string> Disabled { get; } =
        (disabledFeatures ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(feature => feature.ToUpperInvariant())
                                .ToHashSet();

    public static FeatureOverrides FromEnvironment()
    {
        return new FeatureOverrides(Environment.GetEnvironmentVariable(EnvironmentKey));
    }

    public bool IsDisabled(string featureKey)
    {
        return Disabled.Contains(featureKey.ToUpperInvariant());
    }
}
