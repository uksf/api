namespace UKSF.Api.Modpack.WorkshopModProcessing;

public record OperationResult(bool Success, string ErrorMessage = null, bool InterventionRequired = false, bool FilesChanged = true)
{
    public static OperationResult Successful(bool interventionRequired = false, bool filesChanged = true) =>
        new(true, InterventionRequired: interventionRequired, FilesChanged: filesChanged);

    public static OperationResult Failure(string errorMessage) => new(false, errorMessage);
}
