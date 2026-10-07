namespace UKSF.Api.Core.Services;

public sealed class VerifyMode(string? flag, string? emailDirectory)
{
    public const string EnvironmentKey = "UKSF_VERIFY_MODE";
    public const string EmailDirectoryKey = "UKSF_VERIFY_EMAIL_DIR";

    public bool Enabled { get; } = flag is "1" || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);

    public string EmailDirectory { get; } = string.IsNullOrWhiteSpace(emailDirectory) ? Path.Combine(Path.GetTempPath(), "uksf-verify-email") : emailDirectory;

    public static VerifyMode FromEnvironment()
    {
        return new VerifyMode(Environment.GetEnvironmentVariable(EnvironmentKey), Environment.GetEnvironmentVariable(EmailDirectoryKey));
    }
}
