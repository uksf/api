using FluentAssertions;
using UKSF.Api.Integrations.Teamspeak.Query;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Query;

public class TeamspeakQueryProtocolTests
{
    [Fact]
    public void Escape_encodes_every_reserved_character()
    {
        TeamspeakQueryProtocol.Escape("a b|c/d\\e\nf\tg").Should().Be("a\\sb\\pc\\/d\\\\e\\nf\\tg");
    }

    [Fact]
    public void Unescape_reverses_escape()
    {
        const string Message = "\n===== UKSF =====\nHello Sgt.Smith | test / path \\ back\ttab";

        TeamspeakQueryProtocol.Unescape(TeamspeakQueryProtocol.Escape(Message)).Should().Be(Message);
    }

    [Fact]
    public void ParseRecords_splits_records_and_unescapes_values()
    {
        var records = TeamspeakQueryProtocol.ParseRecords(
            "clid=1 client_nickname=Sgt.Smith client_servergroups=6,8|clid=2 client_nickname=Cpl\\sJones client_away"
        );

        records.Should().HaveCount(2);
        records[0]["client_servergroups"].Should().Be("6,8");
        records[1]["client_nickname"].Should().Be("Cpl Jones");
        records[1]["client_away"].Should().BeEmpty();
    }

    [Fact]
    public void ParseRecords_returns_nothing_for_an_empty_line()
    {
        TeamspeakQueryProtocol.ParseRecords("").Should().BeEmpty();
    }

    [Fact]
    public void ThrowIfError_throws_with_the_error_id_and_message()
    {
        var act = () => TeamspeakQueryProtocol.ThrowIfError("error id=2560 msg=invalid\\sgroupID");

        act.Should().Throw<TeamspeakQueryException>().Where(x => x.Id == 2560 && x.Message == "TeamSpeak query error 2560: invalid groupID");
    }

    [Fact]
    public void ThrowIfError_accepts_ok()
    {
        var act = () => TeamspeakQueryProtocol.ThrowIfError("error id=0 msg=ok");

        act.Should().NotThrow();
    }
}
