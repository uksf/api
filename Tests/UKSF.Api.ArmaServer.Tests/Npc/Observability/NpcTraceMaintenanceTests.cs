using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Npc.Observability;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc.Observability;

public class NpcTraceMaintenanceTests
{
    private static BsonDocument Utterance(string uid, string name, string text) =>
        new()
        {
            { "_id", $"p:s1:{uid}" },
            { "type", "utterance.received" },
            { "session", "s1" },
            {
                "data", new BsonDocument
                {
                    { "uid", uid },
                    { "name", name },
                    { "text", text }
                }
            }
        };

    [Fact]
    public void Scrub_ReplacesUidAndNameEverywhere_WithStablePseudonyms()
    {
        var events = new List<BsonDocument>
        {
            Utterance("76561198000000001", "Beswick.T", "Tomas, where are the trucks?"),
            Utterance("76561198000000002", "Smith", "beswick.t is asking you"),
            new()
            {
                { "_id", "p:s1:9" },
                { "type", "turn.replied" },
                { "session", "s1" },
                {
                    "data",
                    new BsonDocument
                    {
                        { "writer", new BsonArray { new BsonDocument("request", "Soldier Beswick.T (76561198000000001) asked Smith's question") } }
                    }
                }
            }
        };

        var replacements = NpcTraceMaintenance.Pseudonyms(events);
        var first = (BsonDocument)NpcTraceMaintenance.Scrub(events[0], replacements);
        var second = (BsonDocument)NpcTraceMaintenance.Scrub(events[1], replacements);
        var third = (BsonDocument)NpcTraceMaintenance.Scrub(events[2], replacements);

        first["data"]["uid"].AsString.Should().Be("player-1");
        first["data"]["name"].AsString.Should().Be("Player 1");
        first["data"]["text"].AsString.Should().Be("Tomas, where are the trucks?");
        second["data"]["text"].AsString.Should().Be("Player 1 is asking you");
        third["data"]["writer"][0]["request"].AsString.Should().Be("Soldier Player 1 (player-1) asked Player 2's question");
        third["_id"].AsString.Should().Be("p:s1:9");
    }

    [Fact]
    public void Pseudonyms_EmptyWhenNoPlayerSpoke()
    {
        NpcTraceMaintenance.Pseudonyms([new BsonDocument { { "type", "mission.started" }, { "session", "s1" } }]).Should().BeEmpty();
    }

    [Theory]
    [InlineData(10, false, false)]
    [InlineData(31, false, true)]
    [InlineData(31, true, false)]
    [InlineData(12 * 60, true, true)]
    public void IsAbandoned_NeedsQuietAndAStoppedServer_OrLongSilence(int quietMinutes, bool serverRunsIt, bool expected)
    {
        NpcTraceMaintenance.IsAbandoned(System.TimeSpan.FromMinutes(quietMinutes), serverRunsIt).Should().Be(expected);
    }
}
