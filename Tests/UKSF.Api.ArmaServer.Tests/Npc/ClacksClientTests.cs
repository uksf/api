using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class ClacksClientTests
{
    private static (ClacksClient client, List<HttpRequestMessage> sent) Build(HttpStatusCode status, string responseJson)
    {
        var sent = new List<HttpRequestMessage>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .Callback<HttpRequestMessage, CancellationToken>((req, _) => sent.Add(req))
               .ReturnsAsync(new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = "http://dedi-ts:8800" });

        return (new ClacksClient(factory.Object, variables.Object, Mock.Of<IUksfLogger>()), sent);
    }

    [Fact]
    public async Task ChatAsync_PostsModelSystemUserAndParsesResult()
    {
        var (client, sent) = Build(
            HttpStatusCode.OK,
            "{\"model\":\"gpt-5.6-luna\",\"choices\":[{\"message\":{\"content\":\"Get back.\"}}],\"_clacks\":{\"node\":\"server\",\"ms\":1400}}"
        );
        var result = await client.ChatAsync("npc", "SYS", "USR", json: true, maxTokens: 80, temperature: 0.7);

        result.Text.Should().Be("Get back.");
        result.Node.Should().Be("server");
        result.Model.Should().Be("gpt-5.6-luna");

        sent.Should().HaveCount(1);
        sent[0].RequestUri.ToString().Should().Be("http://dedi-ts:8800/v1/chat/completions");
        var body = JsonDocument.Parse(await sent[0].Content.ReadAsStringAsync());
        body.RootElement.GetProperty("model").GetString().Should().Be("luna");
        body.RootElement.GetProperty("effort").GetString().Should().Be("low");
        var fallbacks = body.RootElement.GetProperty("fallbacks").EnumerateArray().Select(e => e.GetString()).ToArray();
        fallbacks.Should().Equal("qwen3.5-9b", "qwen3.5-9b-npc", "haiku");
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        messages[0].GetProperty("content").GetString().Should().Be("SYS");
        messages[1].GetProperty("content").GetString().Should().Be("USR");
        body.RootElement.GetProperty("json").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(80);
    }

    [Fact]
    public async Task ChatAsync_ReturnsNullWhenClacksUrlNotConfigured()
    {
        var factory = new Mock<IHttpClientFactory>();
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = null });
        var client = new ClacksClient(factory.Object, variables.Object, Mock.Of<IUksfLogger>());

        var result = await client.ChatAsync("npc", "s", "u", false, 80, 0.7);

        result.Should().BeNull();
        factory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChatAsync_ReturnsNullOnHttpFailure()
    {
        var (client, _) = Build(HttpStatusCode.ServiceUnavailable, "{\"error\":\"no route\"}");
        var result = await client.ChatAsync("npc", "s", "u", false, 80, 0.7);
        result.Should().BeNull();
    }

    [Fact]
    public async Task SpeakAsync_PostsRoleTextVoiceIdAndParsesResult()
    {
        var (client, sent) = Build(HttpStatusCode.OK, "{\"audioBase64\":\"V0FW\",\"durationMs\":2500,\"node\":\"ultron\",\"model\":\"kokoro\",\"ms\":2600}");
        var result = await client.SpeakAsync("npc-voice", "Get back.", "bm_george");

        result.AudioBase64.Should().Be("V0FW");
        result.DurationMs.Should().Be(2500);
        result.Node.Should().Be("ultron");
        result.Model.Should().Be("kokoro");

        sent.Should().HaveCount(1);
        sent[0].RequestUri.ToString().Should().Be("http://dedi-ts:8800/speak");
        var body = JsonDocument.Parse(await sent[0].Content.ReadAsStringAsync());
        body.RootElement.GetProperty("model").GetString().Should().Be("pockettts");
        var nodes = body.RootElement.GetProperty("nodes").EnumerateArray().Select(e => e.GetString()).ToArray();
        nodes.Should().Equal("server", "ultron", "iultron");
        body.RootElement.GetProperty("text").GetString().Should().Be("Get back.");
        body.RootElement.GetProperty("voiceId").GetString().Should().Be("bm_george");
    }

    [Fact]
    public async Task SpeakAsync_ReturnsNullOnHttpFailure()
    {
        var (client, _) = Build(HttpStatusCode.BadGateway, "{\"error\":\"voicebox generation failed\"}");
        var result = await client.SpeakAsync("npc-voice", "t", "v");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SpeakAsync_ReturnsNullWhenClacksUrlNotConfigured()
    {
        var factory = new Mock<IHttpClientFactory>();
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = null });
        var client = new ClacksClient(factory.Object, variables.Object, Mock.Of<IUksfLogger>());

        var result = await client.SpeakAsync("npc-voice", "t", "v");

        result.Should().BeNull();
        factory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }
}
