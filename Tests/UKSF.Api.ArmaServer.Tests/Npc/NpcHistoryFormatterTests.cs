using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcHistoryFormatterTests
{
    [Fact]
    public void EmptyHistoryIsEmptyString() => NpcHistoryFormatter.Format([]).Should().BeEmpty();

    [Fact]
    public void FormatsPlayerAndNpcLinesOldestFirst()
    {
        var entries = new List<NpcHistoryEntry>
        {
            new()
            {
                Role = "player",
                Speaker = "Alpha",
                Text = "where is the cache?",
                T = 1
            },
            new()
            {
                Role = "npc",
                Speaker = "",
                Text = "I know nothing.",
                T = 2
            },
            new()
            {
                Role = "player",
                Speaker = "Bravo",
                Text = "tell us now",
                T = 3
            }
        };

        NpcHistoryFormatter.Format(entries).Should().Be("Alpha: where is the cache?\nNPC: I know nothing.\nBravo: tell us now");
    }
}
