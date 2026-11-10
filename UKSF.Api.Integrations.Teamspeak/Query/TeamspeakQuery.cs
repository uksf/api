using System.Net.Sockets;
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

public sealed class TeamspeakQuery(IOptions<AppSettings> options) : ITeamspeakQuery, IDisposable
{
    private const int NicknameInUseErrorId = 513;
    private const string Nickname = "UKSF";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly AppSettings.SecretsConfig.TeamspeakQueryConfig _config = options.Value.Secrets?.TeamspeakQuery;
    private TeamspeakQueryConnection _connection;

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
        _connection?.Dispose();
        _connection = null;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> Execute(string command)
    {
        if (!Configured)
        {
            throw new InvalidOperationException("TeamSpeak query login is not configured");
        }

        await _lock.WaitAsync();
        try
        {
            _connection ??= await Connect();
            return await _connection.Execute(command);
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException)
        {
            Disconnect();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<TeamspeakQueryConnection> Connect()
    {
        var connection = await TeamspeakQueryConnection.Open(_config.Host ?? "127.0.0.1", _config.Port ?? 10011);
        try
        {
            await connection.Execute($"login client_login_name={Escape(_config.Username)} client_login_password={Escape(_config.Password ?? string.Empty)}");
            await connection.Execute($"use port={_config.ServerPort ?? 9987}");
            await SetNickname(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static async Task SetNickname(TeamspeakQueryConnection connection)
    {
        try
        {
            await connection.Execute($"clientupdate client_nickname={Escape(Nickname)}");
        }
        catch (TeamspeakQueryException exception) when (exception.Id == NicknameInUseErrorId) { }
    }
}
