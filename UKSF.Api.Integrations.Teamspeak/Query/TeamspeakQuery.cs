using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using static UKSF.Api.Integrations.Teamspeak.Query.TeamspeakQueryProtocol;

namespace UKSF.Api.Integrations.Teamspeak.Query;

public record TeamspeakQueryClient(int ClientId, int ClientDbId, string Nickname, int ChannelId, IReadOnlyList<int> ServerGroups);

public interface ITeamspeakQuery
{
    bool Configured { get; }
    Task<IReadOnlyList<TeamspeakQueryClient>> GetClients();
    Task<IReadOnlyDictionary<int, string>> GetChannelNames();
    Task<IReadOnlyList<int>> GetServerGroups(int clientDbId);
    Task AddServerGroup(int clientDbId, int serverGroupId);
    Task RemoveServerGroup(int clientDbId, int serverGroupId);
    Task SendMessage(int clientId, string message);
    void Disconnect();
}

public sealed class TeamspeakQuery(IOptions<AppSettings> options, TimeSpan operationTimeout) : ITeamspeakQuery, IDisposable
{
    private const int NicknameInUseErrorId = 513;
    private const string Nickname = "UKSF";
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(15);
    private readonly AppSettings.SecretsConfig.TeamspeakQueryConfig _config = options.Value.Secrets?.TeamspeakQuery;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource _disconnecting = new();
    private TeamspeakQueryConnection _connection;

    public TeamspeakQuery(IOptions<AppSettings> options) : this(options, DefaultOperationTimeout) { }

    public bool Configured => !string.IsNullOrEmpty(_config?.Username);

    public void Dispose()
    {
        Disconnect();
        _lock.Dispose();
    }

    public async Task<IReadOnlyList<TeamspeakQueryClient>> GetClients()
    {
        var records = await Execute("clientlist -groups");
        return records.Where(x => x["client_type"] == "0")
                      .Select(x => new TeamspeakQueryClient(
                                  int.Parse(x["clid"]),
                                  int.Parse(x["client_database_id"]),
                                  x["client_nickname"],
                                  int.Parse(x["cid"]),
                                  x["client_servergroups"].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                              )
                      )
                      .ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetChannelNames()
    {
        var records = await Execute("channellist");
        return records.ToDictionary(x => int.Parse(x["cid"]), x => x["channel_name"]);
    }

    public async Task<IReadOnlyList<int>> GetServerGroups(int clientDbId)
    {
        var records = await Execute($"servergroupsbyclientid cldbid={clientDbId}");
        return records.Select(x => int.Parse(x["sgid"])).ToList();
    }

    public Task AddServerGroup(int clientDbId, int serverGroupId)
    {
        return Execute($"servergroupaddclient sgid={serverGroupId} cldbid={clientDbId}");
    }

    public Task RemoveServerGroup(int clientDbId, int serverGroupId)
    {
        return Execute($"servergroupdelclient sgid={serverGroupId} cldbid={clientDbId}");
    }

    public Task SendMessage(int clientId, string message)
    {
        return Execute($"sendtextmessage targetmode=1 target={clientId} msg={Escape(message)}");
    }

    public void Disconnect()
    {
        Interlocked.Exchange(ref _disconnecting, new CancellationTokenSource()).Cancel();
        _lock.Wait();
        try
        {
            DisposeConnection();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void DisposeConnection()
    {
        _connection?.Dispose();
        _connection = null;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> Execute(string command)
    {
        if (!Configured)
        {
            throw new InvalidOperationException("TeamSpeak query login is not configured");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_disconnecting.Token);
        deadline.CancelAfter(operationTimeout);
        await _lock.WaitAsync(deadline.Token);
        try
        {
            _connection ??= await Connect(deadline.Token);
            return await _connection.Execute(command, deadline.Token);
        }
        catch (Exception exception) when (exception is not TeamspeakQueryException)
        {
            DisposeConnection();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<TeamspeakQueryConnection> Connect(CancellationToken cancellationToken)
    {
        var connection = await TeamspeakQueryConnection.Open(_config.Host ?? "127.0.0.1", _config.Port ?? 10011, cancellationToken);
        try
        {
            await connection.Execute(
                $"login client_login_name={Escape(_config.Username)} client_login_password={Escape(_config.Password ?? string.Empty)}",
                cancellationToken
            );
            await connection.Execute($"use port={_config.ServerPort ?? 9987}", cancellationToken);
            await SetNickname(connection, cancellationToken);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static async Task SetNickname(TeamspeakQueryConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await connection.Execute($"clientupdate client_nickname={Escape(Nickname)}", cancellationToken);
        }
        catch (TeamspeakQueryException exception) when (exception.Id == NicknameInUseErrorId) { }
    }
}
