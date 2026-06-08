using System;
using System.Net;
using System.Net.Http;
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

public class ClacksClientEmoteTests
{
    private static ClacksClient Build(HttpStatusCode code, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage(code) { Content = new StringContent(body) });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));
        var vars = new Mock<IVariablesService>();
        vars.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = "http://localhost:8800" });
        return new ClacksClient(factory.Object, vars.Object, new Mock<IUksfLogger>().Object);
    }

    [Fact]
    public async Task Ok_decodes_audio_bytes()
    {
        var client = Build(HttpStatusCode.OK, "{\"audioBase64\":\"AAAA\",\"durationMs\":500}");
        var r = await client.EmoteAsync("smuggler", "Get out.", "furious", 0.8);
        r.Status.Should().Be(EmoteStatus.Ok);
        r.WavBytes.Should().Equal(Convert.FromBase64String("AAAA"));
        r.DurationMs.Should().Be(500);
    }

    [Fact]
    public async Task Status503_is_NodeDown()
    {
        var client = Build(HttpStatusCode.ServiceUnavailable, "{\"error\":\"no route for role\"}");
        (await client.EmoteAsync("smuggler", "t", "e", 0.8)).Status.Should().Be(EmoteStatus.NodeDown);
    }

    [Fact]
    public async Task Other_errors_are_Failed()
    {
        var client = Build(HttpStatusCode.BadGateway, "{\"error\":\"gen blew up\"}");
        (await client.EmoteAsync("smuggler", "t", "e", 0.8)).Status.Should().Be(EmoteStatus.Failed);
    }
}
