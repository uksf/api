namespace UKSF.Api.Modpack.WorkshopModProcessing;

public record OperationResult(
    bool Success,
    string ErrorMessage = null,
    bool InterventionRequired = false,
    bool FilesChanged = true,
    List<string> AvailablePbos = null
)
{
    public static OperationResult Successful(bool interventionRequired = false, bool filesChanged = true, List<string> availablePbos = null) =>
        new(true, InterventionRequired: interventionRequired, FilesChanged: filesChanged, AvailablePbos: availablePbos);

    public static OperationResult Failure(string errorMessage) => new(false, errorMessage);
}
