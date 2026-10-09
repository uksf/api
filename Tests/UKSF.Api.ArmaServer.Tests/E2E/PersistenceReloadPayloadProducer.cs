using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;
using Xunit.Abstractions;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Tests.E2E;

public class PersistenceReloadPayloadProducer
{
    private const string CapturePath = @"D:\Arma\persistence-e2e\captured\stage1.json";
    private const string ReloadOutPath = @"D:\Arma\persistence-e2e\captured\stage2-reload.sqf";

    private readonly ITestOutputHelper _output;
    public PersistenceReloadPayloadProducer(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ProduceReloadPayload()
    {
        if (!File.Exists(CapturePath))
        {
            _output.WriteLine($"SKIP: {CapturePath} missing — run tools/run-stage1.js first");
            return;
        }

        var raw = File.ReadAllText(CapturePath);
        using var doc = JsonDocument.Parse(raw);
        var resultStr = doc.RootElement.GetProperty("result").GetString()!;
        var outer = ToDict(SqfNotationParser.ParseAndNormalize(resultStr));
        var payload = outer["payload"].ToString()!;
        var key = outer["key"].ToString()!;
        var sessionId = outer["sessionId"].ToString()!;

        DomainPersistenceSession captured = null;
        var ctx = new Mock<IPersistenceSessionsContext>();
        ctx.Setup(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), It.IsAny<string>()))
           .Callback<DomainPersistenceSession, string>((s, _) => captured = s)
           .ReturnsAsync(true);

        var service = new PersistenceSessionsService(ctx.Object, new Mock<IUksfLogger>().Object);
        var sessionDict = ToDict(SqfNotationParser.ParseAndNormalize(payload));
        await service.HandleSaveAsync(key, sessionId, sessionDict);
        captured.Should().NotBeNull();

        var reloadHashmap = PersistenceConverter.ToHashmap(captured!);
        var reloadSqf = SqfNotationWriter.Write(reloadHashmap);

        Directory.CreateDirectory(Path.GetDirectoryName(ReloadOutPath)!);
        File.WriteAllText(ReloadOutPath, reloadSqf);

        _output.WriteLine($"reload payload: {reloadSqf.Length} chars → {ReloadOutPath}");
        _output.WriteLine($"players: {captured!.Players.Count}, objects: {captured.Objects.Count}, deleted: {captured.DeletedObjects.Count}");
        _output.WriteLine($"customData keys: [{string.Join(",", captured.CustomData.Keys)}]");
    }
}
