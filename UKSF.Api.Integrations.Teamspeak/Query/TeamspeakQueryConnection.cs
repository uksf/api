using System.Net.Sockets;
using System.Text;

namespace UKSF.Api.Integrations.Teamspeak.Query;

public sealed class TeamspeakQueryConnection : IDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private TeamspeakQueryConnection(TcpClient client)
    {
        _client = client;
        var stream = client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
    }

    public void Dispose()
    {
        _reader.Dispose();
        _writer.Dispose();
        _client.Dispose();
    }

    public static async Task<TeamspeakQueryConnection> Open(string host, int port)
    {
        using var timeout = new CancellationTokenSource(CommandTimeout);
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
            var connection = new TeamspeakQueryConnection(client);
            while (!(await connection.ReadLine(timeout.Token)).StartsWith("Welcome", StringComparison.Ordinal)) { }

            return connection;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> Execute(string command)
    {
        using var timeout = new CancellationTokenSource(CommandTimeout);
        await _writer.WriteLineAsync(command.AsMemory(), timeout.Token);

        var data = string.Empty;
        while (true)
        {
            var line = await ReadLine(timeout.Token);
            if (line.Length == 0 || line.StartsWith("notify", StringComparison.Ordinal))
            {
                continue;
            }

            if (!line.StartsWith("error ", StringComparison.Ordinal))
            {
                data = line;
                continue;
            }

            try
            {
                TeamspeakQueryProtocol.ThrowIfError(line);
            }
            catch (TeamspeakQueryException exception) when (exception.Id == TeamspeakQueryProtocol.EmptyResultErrorId)
            {
                return [];
            }

            return TeamspeakQueryProtocol.ParseRecords(data);
        }
    }

    private async Task<string> ReadLine(CancellationToken cancellationToken)
    {
        return await _reader.ReadLineAsync(cancellationToken) ?? throw new IOException("TeamSpeak query connection closed");
    }
}
