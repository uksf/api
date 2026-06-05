using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcBrainClientTests
{
    private static (NpcBrainClient client, List<HttpRequestMessage> sent) Build(string responseJson)
    {
        var sent = new List<HttpRequestMessage>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .Callback<HttpRequestMessage, CancellationToken>((req, _) => sent.Add(req))
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("NPC_BRAIN_URL")).Returns(new DomainVariableItem { Key = "NPC_BRAIN_URL", Item = "http://vm:4400" });

        return (new NpcBrainClient(factory.Object, variables.Object, Mock.Of<IUksfLogger>()), sent);
    }

    [Fact]
    public async Task RespondAsync_PostsToRespondEndpoint_AndParsesResult()
    {
        var (client, sent) = Build("""{"text":"go away","lineId":null,"audioBase64":"AA","durationMs":900,"provider":"claude"}""");

        var result = await client.RespondAsync(new RespondRequest { NpcId = "n1", Mode = "dynamic" });

        sent.Should().ContainSingle();
        sent[0].RequestUri!.ToString().Should().Be("http://vm:4400/npc/respond");
        sent[0].Method.Should().Be(HttpMethod.Post);
        result!.Text.Should().Be("go away");
        result.DurationMs.Should().Be(900);
    }

    [Fact]
    public async Task PrerenderAsync_PostsToPrerenderEndpoint()
    {
        var (client, sent) = Build("""{"items":[{"id":"f0","audioBase64":"QQ==","durationMs":600}]}""");

        var result = await client.PrerenderAsync(new PrerenderRequest { VoiceId = "v", Items = [new PrerenderItem { Id = "f0", Text = "hmm" }] });

        sent[0].RequestUri!.ToString().Should().Be("http://vm:4400/npc/prerender");
        result!.Items.Should().ContainSingle().Which.Id.Should().Be("f0");
    }
}
