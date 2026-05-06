using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace UKSF.Api.ArmaServer.Tests.E2E;

/// <summary>
/// Repro of the ace_medical_logs deserialisation failure surfaced by the e2e harness.
/// Targets the exact SQF shape captured in profile data.
/// </summary>
public class AceMedicalLogShapeRepro
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new WoundEntryConverter(),
            new MedicationEntryConverter(),
            new OccludedMedicationEntryConverter(),
            new IvBagEntryConverter(),
            new TriageCardEntryConverter(),
            new MedicalLogCategoryConverter(),
            new MedicalLogEntryConverter()
        }
    };

    private readonly ITestOutputHelper _output;
    public AceMedicalLogShapeRepro(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DeserializeRealLogShape_RoundsTripsCleanly()
    {
        // Exact JSON the SQF→Dict path produces for one logs category.
        var json = """
                   {
                     "ace_medical_logs": [
                       [
                         "ace_medical_log_activity",
                         [
                           ["STR_ace_medical_treatment_Activity_gaveIV", "5:56", ["Pte.Henri.L"], "activity"],
                           ["STR_ace_medical_treatment_Activity_CPR",    "6:04", ["Pte.Whitby.A"], "activity"]
                         ]
                       ]
                     ]
                   }
                   """;

        var act = () => JsonSerializer.Deserialize<AceMedicalState>(json, Opts);
        var state = act.Should().NotThrow().Subject;

        state!.Logs.Should().HaveCount(1);
        state.Logs[0].LogType.Should().Be("ace_medical_log_activity");
        state.Logs[0].Entries.Should().HaveCount(2);
        state.Logs[0].Entries[0].Message.Should().Be("STR_ace_medical_treatment_Activity_gaveIV");
        state.Logs[0].Entries[0].Timestamp.Should().Be("5:56");
        state.Logs[0].Entries[0].Arguments.Should().BeEquivalentTo("Pte.Henri.L");
        state.Logs[0].Entries[0].LogType.Should().Be("activity");

        _output.WriteLine($"OK: {state.Logs[0].Entries.Count} entries parsed");
    }
}
