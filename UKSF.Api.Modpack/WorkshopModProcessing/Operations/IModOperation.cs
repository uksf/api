namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public interface IModOperation
{
    Task<OperationResult> DownloadAsync(string workshopModId, CancellationToken cancellationToken = default);
    Task<OperationResult> CheckAsync(string workshopModId, CancellationToken cancellationToken = default);
    Task<OperationResult> ExecuteAsync(string workshopModId, List<string> selectedPbos, CancellationToken cancellationToken = default);
}

public interface IInstallOperation : IModOperation;

public interface IUpdateOperation : IModOperation;

public interface IUninstallOperation : IModOperation;
