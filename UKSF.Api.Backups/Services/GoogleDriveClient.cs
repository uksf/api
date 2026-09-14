using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using UKSF.Api.Backups.Models;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.Services;

public interface IGoogleDriveClient
{
    Task<BackupCloudFile> Upload(string localPath, string fileName, CancellationToken cancellationToken = default);
    Task<List<BackupCloudFile>> List(CancellationToken cancellationToken = default);
    Task Delete(string fileId, CancellationToken cancellationToken = default);
    Task<BackupCloudQuota> GetQuota(CancellationToken cancellationToken = default);
}

/// <summary>
///     Thin adapter over the Drive SDK. All policy - what to keep, what to delete, whether there is room - lives in
///     <see cref="BackupRetentionService" /> so it can be tested without a Google account.
/// </summary>
public class GoogleDriveClient(IVariablesService variablesService, IFileSystemProvider fileSystemProvider) : IGoogleDriveClient
{
    private const string ApplicationName = "UKSF Backups";
    private const string FileFields = "id, name, size, createdTime";
    private const string MimeType = "application/octet-stream";

    public async Task<BackupCloudFile> Upload(string localPath, string fileName, CancellationToken cancellationToken = default)
    {
        using var service = CreateService();

        var metadata = new Google.Apis.Drive.v3.Data.File { Name = fileName, Parents = [FolderId()] };

        await using var source = fileSystemProvider.OpenRead(localPath);
        var request = service.Files.Create(metadata, source, MimeType);
        request.Fields = FileFields;
        request.ChunkSize = Google.Apis.Upload.ResumableUpload.MinimumChunkSize * 4;

        var progress = await request.UploadAsync(cancellationToken);
        if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
        {
            throw new UksfException($"Backup upload to Drive failed: {progress.Exception?.Message}", 500);
        }

        return ToCloudFile(request.ResponseBody);
    }

    public async Task<List<BackupCloudFile>> List(CancellationToken cancellationToken = default)
    {
        using var service = CreateService();

        var request = service.Files.List();
        request.Q = $"'{FolderId()}' in parents and trashed = false";
        request.Fields = $"files({FileFields})";
        request.PageSize = 100;

        var response = await request.ExecuteAsync(cancellationToken);
        return response.Files.Select(ToCloudFile).ToList();
    }

    public async Task Delete(string fileId, CancellationToken cancellationToken = default)
    {
        using var service = CreateService();
        await service.Files.Delete(fileId).ExecuteAsync(cancellationToken);
    }

    public async Task<BackupCloudQuota> GetQuota(CancellationToken cancellationToken = default)
    {
        using var service = CreateService();

        var request = service.About.Get();
        request.Fields = "storageQuota";
        var about = await request.ExecuteAsync(cancellationToken);

        return new BackupCloudQuota { LimitBytes = about.StorageQuota.Limit ?? long.MaxValue, UsedBytes = about.StorageQuota.Usage ?? 0 };
    }

    private static BackupCloudFile ToCloudFile(Google.Apis.Drive.v3.Data.File file)
    {
        return new BackupCloudFile
        {
            Id = file.Id,
            Name = file.Name,
            Bytes = file.Size ?? 0,
            CreatedUtc = file.CreatedTimeDateTimeOffset?.UtcDateTime ?? DateTime.MinValue
        };
    }

    private string FolderId()
    {
        return Required("BACKUP_DRIVE_FOLDER_ID");
    }

    private DriveService CreateService()
    {
        var flow = new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = Required("BACKUP_DRIVE_CLIENT_ID"), ClientSecret = Required("BACKUP_DRIVE_CLIENT_SECRET") },
                Scopes = [DriveService.ScopeConstants.DriveFile]
            }
        );

        var credential = new UserCredential(flow, ApplicationName, new TokenResponse { RefreshToken = Required("BACKUP_DRIVE_REFRESH_TOKEN") });

        return new DriveService(new BaseClientService.Initializer { HttpClientInitializer = credential, ApplicationName = ApplicationName });
    }

    private string Required(string key)
    {
        var value = variablesService.GetVariable(key).AsStringWithDefault(null);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UksfException($"Backup cloud upload is not configured - variable '{key}' is missing", 500);
        }

        return value;
    }
}
