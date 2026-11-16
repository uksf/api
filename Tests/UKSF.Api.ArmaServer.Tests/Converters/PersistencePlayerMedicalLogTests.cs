using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistencePlayerMedicalLogTests
{
    [Fact]
    public void FromHashmap_WithNumericMedicalLogArguments_ShouldKeepThemAsText()
    {
        var hashmap = BuildPlayerWithMedicalLogEntry(
            new List<object>
            {
                "STR_ace_medical_log_dose",
                "21:14",
                new List<object>
                {
                    "Pte.Smith.J",
                    2L,
                    0.5,
                    true
                },
                "quick_view"
            }
        );

        var entry = PersistencePlayerConverter.FromHashmap(hashmap).AceMedical.Logs[0].Entries[0];

        entry.Message.Should().Be("STR_ace_medical_log_dose");
        entry.Timestamp.Should().Be("21:14");
        entry.Arguments.Should().Equal("Pte.Smith.J", "2", "0.5", "true");
        entry.LogType.Should().Be("quick_view");
    }

    [Fact]
    public void FromHashmap_WithNumericMedicalLogTimestamp_ShouldKeepItAsText()
    {
        var hashmap = BuildPlayerWithMedicalLogEntry(
            new List<object>
            {
                "STR_ace_medical_log_cpr",
                754.25,
                new List<object>(),
                "activity"
            }
        );

        var entry = PersistencePlayerConverter.FromHashmap(hashmap).AceMedical.Logs[0].Entries[0];

        entry.Timestamp.Should().Be("754.25");
        entry.Arguments.Should().BeEmpty();
    }

    private static Dictionary<string, object> BuildPlayerWithMedicalLogEntry(List<object> entry) =>
        new()
        {
            ["position"] = new List<object>
            {
                0.0,
                0.0,
                0.0
            },
            ["vehicleState"] = new List<object>
            {
                "",
                "",
                -1L
            },
            ["direction"] = 0.0,
            ["animation"] = "",
            ["loadout"] = new List<object>
            {
                new List<object>(),
                new List<object>(),
                new List<object>(),
                new List<object>(),
                new List<object>(),
                new List<object>(),
                "",
                "",
                new List<object>(),
                new List<object>
                {
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                }
            },
            ["damage"] = 0.0,
            ["aceMedical"] =
                new Dictionary<string, object>
                {
                    ["ace_medical_logs"] = new Dictionary<string, object> { ["ace_medical_log_quick_view"] = new List<object> { entry } }
                },
            ["earplugs"] = false,
            ["attachedItems"] = new List<object>(),
            ["radios"] = new List<object>(),
            ["diveState"] = new List<object> { false }
        };
}
