using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Integrations.Teamspeak.Query;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Query;

public sealed class TeamspeakQueryTests : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly List<string> _received = [];
    private readonly Dictionary<string, string> _replies = [];
    private readonly Task<bool> _serverSawClose;

    public TeamspeakQueryTests()
    {
        _listener.Start();
        _serverSawClose = Serve();
    }

    public void Dispose()
    {
        _listener.Stop();
    }

    [Fact]
    public async Task Reads_records_from_a_server_reply()
    {
        _replies["login"] = "error id=0 msg=ok";
        _replies["use"] = "error id=0 msg=ok";
        _replies["clientupdate"] = "error id=0 msg=ok";
        _replies["channellist"] = "cid=3 channel_name=Briefing\\sRoom|cid=4 channel_name=Range\nerror id=0 msg=ok";
        using var subject = CreateSubject(TimeSpan.FromSeconds(5));

        var channels = await subject.GetChannelNames();

        channels.Should().BeEquivalentTo(new Dictionary<int, string> { [3] = "Briefing Room", [4] = "Range" });
    }

    [Fact]
    public async Task A_stalled_server_fails_the_operation_at_its_deadline()
    {
        using var subject = CreateSubject(TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();

        var act = () => subject.GetChannelNames();

        await act.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Disconnect_cancels_the_running_and_the_queued_operation_and_closes_the_socket()
    {
        using var subject = CreateSubject(TimeSpan.FromSeconds(60));
        var running = subject.GetChannelNames();
        await WaitUntil(() => _received.Count > 0);
        var queued = subject.GetClients();

        subject.Disconnect();

        await FluentActions.Awaiting(() => running.WaitAsync(TimeSpan.FromSeconds(5))).Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => queued.WaitAsync(TimeSpan.FromSeconds(5))).Should().ThrowAsync<OperationCanceledException>();
        (await _serverSawClose.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        _received.Should().ContainSingle().Which.Should().StartWith("login ");
    }

    private TeamspeakQuery CreateSubject(TimeSpan timeout)
    {
        var settings = new AppSettings
        {
            Secrets = new AppSettings.SecretsConfig
            {
                TeamspeakQuery = new AppSettings.SecretsConfig.TeamspeakQueryConfig
                {
                    Host = "127.0.0.1",
                    Port = ((IPEndPoint)_listener.LocalEndpoint).Port,
                    Username = "probe",
                    Password = "secret"
                }
            }
        };
        return new TeamspeakQuery(Options.Create(settings), timeout);
    }

    private async Task<bool> Serve()
    {
        using var client = await _listener.AcceptTcpClientAsync();
        using var reader = new StreamReader(client.GetStream());
        await using var writer = new StreamWriter(client.GetStream()) { NewLine = "\n", AutoFlush = true };
        await writer.WriteLineAsync("TS3");
        await writer.WriteLineAsync("Welcome to the TeamSpeak 3 ServerQuery interface.");
        while (await reader.ReadLineAsync() is { } line)
        {
            lock (_received)
            {
                _received.Add(line);
            }

            if (_replies.TryGetValue(line.Split(' ')[0], out var reply))
            {
                await writer.WriteLineAsync(reply);
            }
        }

        return true;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            await Task.Delay(20);
        }
    }
}
