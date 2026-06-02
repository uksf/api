using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Services;

public interface ISteamCmdService
{
    Task<string> GetServerInfo();
    Task<string> UpdateServer();
    Task<string> DownloadWorkshopMod(string workshopModId);
    Task<string> RefreshLogin();
}

public class SteamCmdService : ISteamCmdService
{
    private const int MaxLoginAttempts = 3;

    private readonly string _password;
    private readonly string _username;
    private readonly IVariablesService _variablesService;
    private readonly ISteamGuardCodeService _steamGuardCodeService;
    private readonly IUksfLogger _logger;

    public SteamCmdService(IVariablesService variablesService, IOptions<AppSettings> options, ISteamGuardCodeService steamGuardCodeService, IUksfLogger logger)
    {
        _variablesService = variablesService;
        _steamGuardCodeService = steamGuardCodeService;
        _logger = logger;

        var appSettings = options.Value;
        _username = appSettings.Secrets.SteamCmd.Username;
        _password = appSettings.Secrets.SteamCmd.Password;
    }

    public async Task<string> GetServerInfo()
    {
        return await ExecuteSteamCmd("+login anonymous +app_info_update 1 +app_info_print 233780 +logoff +quit");
    }

    public async Task<string> UpdateServer()
    {
        return await ExecuteAuthenticatedSteamCmd("+\"app_update 233780 -beta creatordlc\" validate +quit");
    }

    public async Task<string> DownloadWorkshopMod(string workshopModId)
    {
        var output = await ExecuteAuthenticatedSteamCmd($"+workshop_download_item 107410 {workshopModId} +quit");

        if (IsDownloadFailure(output))
        {
            throw new Exception(output);
        }

        return output;
    }

    private static readonly string[] DownloadFailureMarkers =
    [
        "failed", "no connection", "request revoked", "missing game files", "timeout downloading", "canceled", "cancelled"
    ];

    /// <summary>
    ///     True when SteamCMD's workshop download output indicates a failure that should be retried. SteamCMD does not use a
    ///     reliable exit code, so the only signal is its stdout. Matching is case-insensitive and covers the transient
    ///     content-delivery failures observed in practice (no connection, request revoked, missing game files, timeouts) in
    ///     addition to the generic "failed" marker. A null output is treated as a failure.
    /// </summary>
    public static bool IsDownloadFailure(string output)
    {
        return output is null ||
               DownloadFailureMarkers.Any(marker => output.Contains(marker, StringComparison.OrdinalIgnoreCase)) ||
               IsTransientLoginFailure(output);
    }

    public async Task<string> RefreshLogin()
    {
        return await ExecuteAuthenticatedSteamCmd("+quit");
    }

    /// <summary>True when SteamCMD rejected the login because of the Steam Guard code — retryable with a freshly generated code.</summary>
    public static bool IsTransientLoginFailure(string output)
    {
        return output is not null &&
               (output.Contains("Two-factor code mismatch", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("Account Logon Denied", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Runs an authenticated SteamCMD command, retrying when the Steam Guard code is rejected. A rejection is almost
    ///     always a TOTP window boundary race (the code expired between generation and submission), so each retry waits for
    ///     the next code window before generating a fresh code.
    /// </summary>
    public static async Task<string> ExecuteWithCodeRetry(
        Func<Task<string>> execute,
        Func<TimeSpan> timeUntilNextCode,
        Func<TimeSpan, Task> delay,
        bool codeConfigured,
        Action<int> onRetry,
        int maxAttempts = MaxLoginAttempts
    )
    {
        var output = await execute();

        for (var attempt = 1; attempt < maxAttempts && codeConfigured && IsTransientLoginFailure(output); attempt++)
        {
            onRetry(attempt);
            await delay(timeUntilNextCode());
            output = await execute();
        }

        return output;
    }

    private Task<string> ExecuteAuthenticatedSteamCmd(string commandTail)
    {
        return ExecuteWithCodeRetry(
            () => ExecuteSteamCmd($"{BuildLogin()} {commandTail}"),
            () => _steamGuardCodeService.TimeUntilNextCode() + TimeSpan.FromSeconds(1),
            Task.Delay,
            _steamGuardCodeService.IsConfigured,
            attempt => _logger.LogWarning($"Steam Guard code rejected (attempt {attempt}/{MaxLoginAttempts}); retrying with a fresh code")
        );
    }

    private string BuildLogin()
    {
        var guardCode = _steamGuardCodeService.GenerateCode();
        return guardCode is null ? $"+login {_username} {_password}" : $"+login {_username} {_password} {guardCode}";
    }

    private async Task<string> ExecuteSteamCmd(string arguments)
    {
        var steamPath = _variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        var cmdPath = Path.Combine(steamPath, "steamcmd.exe");

        // SteamCMD shares one install directory and one Steam session per machine. Running two processes concurrently
        // races the workshop manifest and triggers Steam to revoke download requests, so every invocation is serialised.
        await SteamCmdGate.WaitAsync();
        try
        {
            var result = await Cli.Wrap(cmdPath)
                                  .WithWorkingDirectory(steamPath)
                                  .WithArguments(arguments)
                                  .WithValidation(CommandResultValidation.None)
                                  .ExecuteBufferedAsync();

            return result.StandardOutput;
        }
        finally
        {
            SteamCmdGate.Release();
        }
    }

    private static readonly SemaphoreSlim SteamCmdGate = new(1, 1);
}
