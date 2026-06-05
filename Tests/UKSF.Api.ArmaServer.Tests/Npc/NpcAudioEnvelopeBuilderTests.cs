using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcAudioEnvelopeBuilderTests
{
    [Fact]
    public void SingleChunkAudioEnvelope()
    {
        var cmds = NpcAudioEnvelopeBuilder.BuildAudio("npc1", "turn7", "QUJD", 1500, 48000);
        cmds.Should().ContainSingle();
        cmds[0].Should().Be("[\"npc_audio\",\"npc1\",\"turn7\",0,1,\"QUJD\",1500]");
    }

    [Fact]
    public void MultiChunkSplitsAndReassembles()
    {
        var b64 = new string('A', 100);
        var cmds = NpcAudioEnvelopeBuilder.BuildAudio("n", "t", b64, 1000, 40);
        cmds.Should().HaveCount(3); // 40 + 40 + 20

        cmds[0].Should().StartWith("[\"npc_audio\",\"n\",\"t\",0,3,\"");
        cmds[2].Should().Contain(",2,3,");

        // Reassemble payloads using a regex that captures the base64 field
        var joined = string.Concat(cmds.Select(ExtractPayload));
        joined.Should().Be(b64);
    }

    [Fact]
    public void FillerEnvelopeShape()
    {
        var cmds = NpcAudioEnvelopeBuilder.BuildFiller("npc1", "bm_george", "f0", "QQ==", 600, 48000);
        cmds.Should().ContainSingle();
        cmds[0].Should().Be("[\"npc_filler\",\"npc1\",\"bm_george\",\"f0\",0,1,\"QQ==\",600]");
    }

    [Fact]
    public void EmptyBase64ProducesOneEmptyChunk()
    {
        var cmds = NpcAudioEnvelopeBuilder.BuildAudio("n", "t", "", 0, 48000);
        cmds.Should().ContainSingle();
        cmds[0].Should().Be("[\"npc_audio\",\"n\",\"t\",0,1,\"\",0]");
    }

    // Extracts the base64 payload field from an npc_audio command.
    // Format: ["npc_audio","<npcId>","<turnId>",<index>,<total>,"<payload>",<durationMs>]
    private static string ExtractPayload(string cmd)
    {
        var match = Regex.Match(cmd, @"^\[""npc_audio"",""[^""]*"",""[^""]*"",\d+,\d+,""(?<p>[^""]*)"",\d+\]$");
        match.Success.Should().BeTrue($"command '{cmd}' should match npc_audio envelope pattern");
        return match.Groups["p"].Value;
    }
}
