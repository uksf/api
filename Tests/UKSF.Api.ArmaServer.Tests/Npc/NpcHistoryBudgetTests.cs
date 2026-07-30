using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcHistoryBudgetTests
{
    private static List<NpcHistoryEntry> Entries(int count, int textLength) =>
        Enumerable.Range(0, count)
                  .Select(i => new NpcHistoryEntry
                      {
                          Role = "player",
                          Speaker = string.Empty,
                          Text = i.ToString().PadRight(textLength, 'x')
                      }
                  )
                  .ToList();

    [Fact]
    public void Keeps_A_Short_Conversation_Whole()
    {
        var history = Entries(6, 50);

        NpcHistoryBudget.Trim(history).Should().BeEquivalentTo(history, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Drops_The_Oldest_Once_Over_Budget()
    {
        var trimmed = NpcHistoryBudget.Trim(Entries(40, 500));

        trimmed.Should().HaveCount(12); // 6000 char budget / 500 per entry
        trimmed.Last().Text.Should().StartWith("39"); // newest survives
        trimmed.First().Text.Should().StartWith("28");
    }

    [Fact]
    public void Keeps_The_Newest_Entry_Even_When_It_Alone_Exceeds_The_Budget()
    {
        var trimmed = NpcHistoryBudget.Trim(Entries(3, 4000));

        trimmed.Should().ContainSingle();
        trimmed[0].Text.Should().StartWith("2");
    }

    [Fact]
    public void Handles_An_Empty_History()
    {
        NpcHistoryBudget.Trim([]).Should().BeEmpty();
        NpcHistoryBudget.Trim(null).Should().BeEmpty();
    }
}
