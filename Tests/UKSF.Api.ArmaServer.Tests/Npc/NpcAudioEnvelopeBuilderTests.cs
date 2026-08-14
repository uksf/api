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

    [Fact]
    public void GuardedState_EscapesQuotes_OmitsFactText_TruncatesFreeText()
    {
        var longReason = new string('r', 400);
        var cmd = NpcAudioEnvelopeBuilder.BuildGuardedState(
            "npc\"1",
            "engaged",
            true,
            false,
            ["f1", "f2"],
            "f2",
            "afraid",
            "looks down",
            longReason,
            "quoted \"span\"",
            12,
            34
        );

        cmd.Should().StartWith("[\"npc_guarded_state\",");
        cmd.Should().Contain("npc\"\"1");
        cmd.Should().Contain("engaged");
        cmd.Should().Contain("true");
        cmd.Should().Contain("f1,f2");
        cmd.Should().Contain("quoted \"\"span\"\"");
        cmd.Should().NotContain("Trucks have been rolling");
        cmd.Length.Should().BeLessThan(4096);
        // free text truncated to 240
        cmd.Should().NotContain(longReason);
    }

    [Fact]
    public void GuardedState_DoesNotDoubleEscapeDisclosedIds()
    {
        var cmd = NpcAudioEnvelopeBuilder.BuildGuardedState("npc1", "engaged", false, false, ["f\"1"], null, "neutral", null, null, null, 0, 0);
        // Quote once → one doubled quote, not quadruple.
        cmd.Should().Contain("f\"\"1");
        cmd.Should().NotContain("f\"\"\"\"1");
    }

    [Fact]
    public void DebugState_Shape_EscapesQuotes_NoDoubleEscape()
    {
        var cmd = NpcAudioEnvelopeBuilder.BuildDebugState(
            "npc\"1",
            "luna@ultron",
            "answer",
            "relevant_question",
            2,
            true,
            false,
            "r",
            "quoted \"span\"",
            12,
            34,
            "f2",
            ["f1", "f2"]
        );

        cmd.Should()
           .Be(
               "[\"npc_debug_state\",\"npc\"\"1\",\"luna@ultron\",\"answer\",\"relevant_question\",\"2\",true,false,\"r\",\"quoted \"\"span\"\"\",12,34,\"f2\",\"f1,f2\"]"
           );
    }

    [Fact]
    public void DebugState_NullOptionals_RenderEmptyQuotedStrings()
    {
        var cmd = NpcAudioEnvelopeBuilder.BuildDebugState("npc1", null, "stay_silent", null, null, false, false, null, null, 0, 0, null, null);

        cmd.Should().Be("[\"npc_debug_state\",\"npc1\",\"\",\"stay_silent\",\"\",\"\",false,false,\"\",\"\",0,0,\"\",\"\"]");
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
