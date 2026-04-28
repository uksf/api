namespace UKSF.Api.ArmaServer.Services;

[Serializable]
public class InvalidModPathException(IReadOnlyList<string> missingPaths) : Exception($"Mod paths do not exist: {string.Join(", ", missingPaths)}")
{
    public IReadOnlyList<string> MissingPaths { get; } = missingPaths;
}
