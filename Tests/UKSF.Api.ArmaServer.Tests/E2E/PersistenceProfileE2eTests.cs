using System;
using System.Collections.Generic;
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

/// <summary>
/// End-to-end test driven by a real captured save payload from a synthetic Arma 3
/// run seeded with profile vars from the Main dedi. Stage 1 (out-of-process) extracts
/// one persistence key from the DeRap'd Main.vars.Arma3Profile, runs the same
/// hashmap-build path as fnc_saveDataApi inside a dev-test-server, and writes the
/// captured SQF str payload to D:/Arma/persistence-e2e/captured/stage1.json.
///
/// This test consumes that file and asserts:
///   1. The API parses the payload without error.
///   2. The DomainPersistenceSession has no shape leaks (no "System.Object[]" leaks,
///      no untyped container items, all weapons routed to Type="weapon").
///   3. SqfNotationWriter round-trip is parser-stable (write → parse → equivalent).
///   4. Counts match the in-game capture (player + object).
///
/// Skipped automatically if stage1.json is missing — run tools/run-stage1.js first.
/// </summary>
public class PersistenceProfileE2eTests
{
    private const string CapturePath = @"D:\Arma\persistence-e2e\captured\stage1.json";

    private readonly ITestOutputHelper _output;

    public PersistenceProfileE2eTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ProfileSnapshot_RoundTripsWithoutShapeLeaks()
    {
        if (!File.Exists(CapturePath))
        {
            _output.WriteLine($"SKIP: {CapturePath} not found. Run: node D:/Arma/persistence-e2e/tools/run-stage1.js");
            return;
        }

        var raw = File.ReadAllText(CapturePath);
        using var doc = JsonDocument.Parse(raw);

        var resultStr = doc.RootElement.GetProperty("result").GetString();
        resultStr.Should().NotBeNullOrEmpty();

        // Outer hashmap ([[k,v],...]) wrapping the captured payload + metadata.
        var outer = ToDict(SqfNotationParser.ParseAndNormalize(resultStr!));
        var payload = outer.GetValueOrDefault("payload")?.ToString();
        var key = outer.GetValueOrDefault("key")?.ToString();
        var sessionId = outer.GetValueOrDefault("sessionId")?.ToString();
        var capturedPlayerCount = ToInt(outer.GetValueOrDefault("playerCount") ?? 0L);
        var capturedObjectCount = ToInt(outer.GetValueOrDefault("objectCount") ?? 0L);

        payload.Should().NotBeNullOrEmpty();
        key.Should().NotBeNullOrEmpty();
        sessionId.Should().NotBeNullOrEmpty();

        _output.WriteLine($"captured payload: {payload!.Length} chars, {capturedPlayerCount} players, {capturedObjectCount} objects, key='{key}'");

        // Drive the save path with a mocked context — we want to assert on the parsed
        // session, not on mongo persistence (which is independently tested).
        DomainPersistenceSession captured = null;
        var mockContext = new Mock<IPersistenceSessionsContext>();
        mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);
        mockContext.Setup(x => x.Add(It.IsAny<DomainPersistenceSession>())).Callback<DomainPersistenceSession>(s => captured = s).Returns(Task.CompletedTask);
        mockContext.Setup(x => x.Replace(It.IsAny<DomainPersistenceSession>()))
                   .Callback<DomainPersistenceSession>(s => captured = s)
                   .Returns(Task.CompletedTask);

        var loggerErrors = new List<string>();
        var mockLogger = new Mock<IUksfLogger>();
        mockLogger.Setup(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()))
                  .Callback<string, Exception>((m, e) => loggerErrors.Add($"{m}: {e.Message}"));
        mockLogger.Setup(x => x.LogWarning(It.IsAny<string>())).Callback<string>(loggerErrors.Add);

        var service = new PersistenceSessionsService(mockContext.Object, mockLogger.Object);
        var sessionDict = ToDict(SqfNotationParser.ParseAndNormalize(payload!));
        await service.HandleSaveAsync(key!, sessionId!, sessionDict);

        loggerErrors.Should().BeEmpty("HandleSaveAsync must not log errors or warnings on a real profile snapshot");
        captured.Should().NotBeNull("HandleSaveAsync must persist the session");

        // ===== Shape leak detection =====
        var leaks = new List<string>();

        foreach (var (uid, player) in captured!.Players)
        {
            ScanContainer(player.Loadout.Uniform, "uniform", uid, leaks);
            ScanContainer(player.Loadout.Vest, "vest", uid, leaks);
            ScanContainer(player.Loadout.Backpack, "backpack", uid, leaks);
        }

        leaks.Should().BeEmpty("no container item should ToString-leak as 'System.Object[]' or hit the unknown-shape path");

        // ===== Count parity =====
        captured.Players.Count.Should().Be(capturedPlayerCount, "API player count should match in-game capture");
        captured.Objects.Count.Should().Be(capturedObjectCount, "API object count should match in-game capture");

        // ===== Round-trip via SqfNotationWriter =====
        var roundTripHashmap = PersistenceConverter.ToHashmap(captured);
        var roundTripSqf = SqfNotationWriter.Write(roundTripHashmap);
        roundTripSqf.Should().StartWith("[", "writer must emit SQF array notation");

        var reparsed = ToDict(SqfNotationParser.ParseAndNormalize(roundTripSqf));
        var reConverted = PersistenceConverter.FromHashmap(reparsed);

        reConverted.Players.Count.Should().Be(captured.Players.Count, "round-trip preserves player count");
        reConverted.Objects.Count.Should().Be(captured.Objects.Count, "round-trip preserves object count");
        reConverted.DeletedObjects.Should().BeEquivalentTo(captured.DeletedObjects);
        reConverted.ArmaDateTime.Should().BeEquivalentTo(captured.ArmaDateTime);

        // Spot-check one player's loadout survives the round trip
        if (captured.Players.Count > 0)
        {
            var (uid, original) = captured.Players.First();
            reConverted.Players.Should().ContainKey(uid);
            var rounded = reConverted.Players[uid];
            rounded.Loadout.PrimaryWeapon.Weapon.Should().Be(original.Loadout.PrimaryWeapon.Weapon);
            rounded.Loadout.Backpack.Items.Count.Should().Be(original.Loadout.Backpack.Items.Count);
            rounded.AceMedical.BloodVolume.Should().Be(original.AceMedical.BloodVolume);
        }

        _output.WriteLine($"round-trip ok: {roundTripSqf.Length} chars emitted, re-parse equivalent");
        _output.WriteLine($"PASS: 0 shape leaks across {captured.Players.Count} players and {captured.Objects.Count} objects");
    }

    private static void ScanContainer(ContainerSlot slot, string slotName, string uid, List<string> leaks)
    {
        if (slot is null || slot.Items is null) return;
        for (var i = 0; i < slot.Items.Count; i++)
        {
            var item = slot.Items[i];
            if (string.IsNullOrEmpty(item.Type))
            {
                leaks.Add($"player {uid} {slotName}[{i}]: empty Type");
                continue;
            }

            if (item.ClassName == "System.Object[]" || (item.ClassName?.StartsWith("System.") ?? false))
            {
                leaks.Add($"player {uid} {slotName}[{i}]: System type leak '{item.ClassName}'");
            }

            if (item.Type == "weapon" && item.Weapon is null)
            {
                leaks.Add($"player {uid} {slotName}[{i}]: weapon Type but null Weapon slot");
            }
        }
    }
}
