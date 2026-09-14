namespace UKSF.Api.Modpack.WorkshopModProcessing;

public record OperationResult(
    bool Success,
    string ErrorMessage = null,
    bool InterventionRequired = false,
    bool FilesChanged = true,
    List<string> AvailablePbos = null,
    List<string> AvailableExtensions = null
)
{
    public static OperationResult Successful(
        bool interventionRequired = false,
        bool filesChanged = true,
        List<string> availablePbos = null,
        List<string> availableExtensions = null
    ) =>
        new(
            true,
            InterventionRequired: interventionRequired,
            FilesChanged: filesChanged,
            AvailablePbos: availablePbos,
            AvailableExtensions: availableExtensions
        );

    public static OperationResult Failure(string errorMessage) => new(false, errorMessage);
}
