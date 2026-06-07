using System.Collections.Generic;
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
    public async Task ChatAsync_PostsRoleSystemUserAndParsesResult()
    {
        var (client, sent) = Build(HttpStatusCode.OK, "{\"text\":\"Get back.\",\"node\":\"server\",\"model\":\"qwen2.5-3b\",\"ms\":1400}");
        var result = await client.ChatAsync("npc", "SYS", "USR", json: true, maxTokens: 80, temperature: 0.7);

        result.Text.Should().Be("Get back.");
        result.Node.Should().Be("server");
        result.Model.Should().Be("qwen2.5-3b");

        sent.Should().HaveCount(1);
        sent[0].RequestUri.ToString().Should().Be("http://dedi-ts:8800/chat");
        var body = JsonDocument.Parse(await sent[0].Content.ReadAsStringAsync());
        body.RootElement.GetProperty("role").GetString().Should().Be("npc");
        body.RootElement.GetProperty("system").GetString().Should().Be("SYS");
        body.RootElement.GetProperty("user").GetString().Should().Be("USR");
        body.RootElement.GetProperty("json").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("maxTokens").GetInt32().Should().Be(80);
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
        var result = await client.SpeakAsync("voice", "Get back.", "bm_george");

        result.AudioBase64.Should().Be("V0FW");
        result.DurationMs.Should().Be(2500);
        result.Node.Should().Be("ultron");
        result.Model.Should().Be("kokoro");

        sent.Should().HaveCount(1);
        sent[0].RequestUri.ToString().Should().Be("http://dedi-ts:8800/speak");
        var body = JsonDocument.Parse(await sent[0].Content.ReadAsStringAsync());
        body.RootElement.GetProperty("role").GetString().Should().Be("voice");
        body.RootElement.GetProperty("text").GetString().Should().Be("Get back.");
        body.RootElement.GetProperty("voiceId").GetString().Should().Be("bm_george");
    }

    [Fact]
    public async Task SpeakAsync_ReturnsNullOnHttpFailure()
    {
        var (client, _) = Build(HttpStatusCode.BadGateway, "{\"error\":\"voicebox generation failed\"}");
        var result = await client.SpeakAsync("voice", "t", "v");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SpeakAsync_ReturnsNullWhenClacksUrlNotConfigured()
    {
        var factory = new Mock<IHttpClientFactory>();
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = null });
        var client = new ClacksClient(factory.Object, variables.Object, Mock.Of<IUksfLogger>());

        var result = await client.SpeakAsync("voice", "t", "v");

        result.Should().BeNull();
        factory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }
}
