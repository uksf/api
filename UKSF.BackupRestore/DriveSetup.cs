using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace UKSF.BackupRestore;

/// <summary>
///     One-time Drive authorisation. Prints the refresh token and the folder id the API needs, and creates the folder
///     itself so the app-scoped `drive.file` permission covers it.
/// </summary>
public static class DriveSetup
{
    private const string ApplicationName = "UKSF Backups";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    public static async Task<int> Run(Arguments arguments)
    {
        var clientId = arguments.Require("client-id");
        var clientSecret = arguments.Require("client-secret");
        var folderName = arguments.Optional("folder") ?? ApplicationName;

        var flow = new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                Scopes = [DriveService.ScopeConstants.DriveFile],
                DataStore = new NullDataStore(),
                Prompt = "consent"
            }
        );

        Console.WriteLine("A browser window will open. Sign in as the account that will hold the backups.");
        var credential = await new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver()).AuthorizeAsync("uksf-backups", CancellationToken.None);

        if (string.IsNullOrWhiteSpace(credential.Token.RefreshToken))
        {
            Console.Error.WriteLine("Google returned no refresh token. Remove the app from the account's third-party access and try again.");
            return 1;
        }

        using var service = new DriveService(new BaseClientService.Initializer { HttpClientInitializer = credential, ApplicationName = ApplicationName });
        var folderId = await EnsureFolder(service, folderName);

        Console.WriteLine();
        Console.WriteLine("Set these as API variables:");
        Console.WriteLine($"  BACKUP_DRIVE_CLIENT_ID      = {clientId}");
        Console.WriteLine($"  BACKUP_DRIVE_CLIENT_SECRET  = {clientSecret}");
        Console.WriteLine($"  BACKUP_DRIVE_REFRESH_TOKEN  = {credential.Token.RefreshToken}");
        Console.WriteLine($"  BACKUP_DRIVE_FOLDER_ID      = {folderId}");
        Console.WriteLine();
        Console.WriteLine("The refresh token is a credential. Do not paste it anywhere but the variables page.");
        return 0;
    }

    private static async Task<string> EnsureFolder(DriveService service, string folderName)
    {
        var list = service.Files.List();
        list.Q = $"name = '{folderName.Replace("'", "\\'")}' and mimeType = '{FolderMimeType}' and trashed = false";
        list.Fields = "files(id, name)";

        var existing = (await list.ExecuteAsync()).Files.FirstOrDefault();
        if (existing is not null)
        {
            Console.WriteLine($"Using existing folder '{existing.Name}'");
            return existing.Id;
        }

        var create = service.Files.Create(new Google.Apis.Drive.v3.Data.File { Name = folderName, MimeType = FolderMimeType });
        create.Fields = "id";
        var created = await create.ExecuteAsync();

        Console.WriteLine($"Created folder '{folderName}'");
        return created.Id;
    }
}
