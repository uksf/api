using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Parsing;
using Xunit;
using Xunit.Abstractions;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Tests.E2E;

/// <summary>
/// Walks every player in the captured profile snapshot and runs ParseAceMedical
/// on each, capturing which players fail and what JSON shape caused the failure.
/// </summary>
public class AceMedicalRealDataDebug
{
    private const string CapturePath = @"D:\Arma\persistence-e2e\captured\stage1.json";

    private readonly ITestOutputHelper _output;
    public AceMedicalRealDataDebug(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ScanAllPlayers_ReportsAceMedicalFailures()
    {
        if (!File.Exists(CapturePath))
        {
            _output.WriteLine("SKIP: capture missing");
            return;
        }

        var raw = File.ReadAllText(CapturePath);
        using var doc = JsonDocument.Parse(raw);
        var resultStr = doc.RootElement.GetProperty("result").GetString()!;
        var outer = ToDict(SqfNotationParser.ParseAndNormalize(resultStr));
        var payload = outer["payload"].ToString()!;

        var sessionDict = ToDict(SqfNotationParser.ParseAndNormalize(payload));
        var players = ToDict(sessionDict["players"]);

        var hashmapShape = 0;
        var stringShape = 0;
        var nullShape = 0;
        var failures = new List<(string uid, string kind, string detail)>();

        // Use reflection to invoke the private ParseAceMedical
        var method = typeof(PersistencePlayerConverter).GetMethod("ParseAceMedical", BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var (uid, playerObj) in players)
        {
            var pdict = ToDict(playerObj);
            var aceMedical = pdict.GetValueOrDefault("aceMedical");

            string kind;
            if (aceMedical is Dictionary<string, object> d)
            {
                hashmapShape++;
                kind = $"hashmap({d.Count})";
            }
            else if (aceMedical is string s)
            {
                stringShape++;
                kind = $"string({s.Length})";
            }
            else if (aceMedical is null)
            {
                nullShape++;
                kind = "null";
            }
            else
            {
                kind = aceMedical.GetType().Name;
            }

            try
            {
                var result = method!.Invoke(null, [aceMedical]);
                if (result is null) failures.Add((uid, kind, "ParseAceMedical returned null"));
            }
            catch (TargetInvocationException tie)
            {
                failures.Add((uid, kind, tie.InnerException?.Message ?? tie.Message));
            }
        }

        _output.WriteLine($"shape distribution: hashmap={hashmapShape} string={stringShape} null={nullShape}");
        _output.WriteLine($"failures: {failures.Count}");
        foreach (var (uid, kind, detail) in failures.Take(10))
        {
            _output.WriteLine($"  {uid} [{kind}]: {detail}");
        }

        // Also: for the FIRST hashmap-shape player, dump the JSON System.Text.Json produces
        var firstHashmap = players.Where(kv => ToDict(kv.Value).GetValueOrDefault("aceMedical") is Dictionary<string, object>).FirstOrDefault();
        if (firstHashmap.Key is not null)
        {
            var d = (Dictionary<string, object>)ToDict(firstHashmap.Value)["aceMedical"];
            var j = JsonSerializer.Serialize(d);
            _output.WriteLine($"\nfirst hashmap player {firstHashmap.Key} aceMedical json (first 500 chars):");
            _output.WriteLine(j.Length > 500 ? j[..500] + "…" : j);
            _output.WriteLine($"\nbyte 127 ± 50 chars: {j.Substring(Math.Max(0, 80), Math.Min(j.Length - 80, 100))}");
        }
    }
}
